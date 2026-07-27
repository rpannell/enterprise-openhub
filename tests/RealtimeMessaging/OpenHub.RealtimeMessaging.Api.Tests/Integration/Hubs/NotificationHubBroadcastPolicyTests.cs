using System.Security.Claims;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using NSubstitute;
using OpenHub.RealtimeMessaging.Api;
using OpenHub.RealtimeMessaging.Api.Tests.Integration.Fixtures;
using Shouldly;
using Xunit;

namespace OpenHub.RealtimeMessaging.Api.Tests.Integration.Hubs;

/// <summary>
/// Verifies the declarative RealtimeMessagingBroadcastPublisher policy on
/// NotificationHub.Broadcast is actually enforced by ASP.NET Core's authorization middleware —
/// something a direct-instantiation unit test of the Hub class cannot cover, since that bypasses
/// the pipeline the [Authorize(Policy=...)] attribute runs in entirely. Uses LongPolling (not
/// WebSockets) against WebApplicationFactory's in-memory TestServer, the standard pattern for
/// testing SignalR hubs this way.
/// </summary>
[Trait("Category", "Integration")]
public class NotificationHubBroadcastPolicyTests : IClassFixture<RealtimeMessagingApiFactory>
{
    private readonly RealtimeMessagingApiFactory _factory;

    public NotificationHubBroadcastPolicyTests(RealtimeMessagingApiFactory factory)
    {
        _factory = factory;
    }

    private HubConnection CreateConnection(IReadOnlyCollection<Claim> claims)
    {
        _factory.ClaimsProvider.Claims = claims;

        var hubUrl = new Uri(_factory.Server.BaseAddress, ApiConst.HubRoute);
        return new HubConnectionBuilder()
            .WithUrl(hubUrl, HttpTransportType.LongPolling, options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
    }

    [Fact]
    public async Task Broadcast_TenantScopedCaller_Rejected()
    {
        await using var connection = CreateConnection(
        [
            new Claim("iss", "http://keycloak.test/realms/client-a"),
            new Claim("sub", "svc-account"),
        ]);
        await connection.StartAsync();

        await Should.ThrowAsync<HubException>(() =>
            connection.InvokeAsync("Broadcast", "smoke-test", "{}", (string?)null));
    }

    [Fact]
    public async Task Broadcast_PlatformAdminCaller_Succeeds()
    {
        _factory.MessageDispatchManagerMock
            .SendBroadcastAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        await using var connection = CreateConnection(
        [
            new Claim("iss", "http://keycloak.test/realms/platform-admin"),
            new Claim("sub", "svc-account"),
        ]);
        await connection.StartAsync();

        await Should.NotThrowAsync(() =>
            connection.InvokeAsync("Broadcast", "smoke-test", "{}", (string?)null));
    }
}
