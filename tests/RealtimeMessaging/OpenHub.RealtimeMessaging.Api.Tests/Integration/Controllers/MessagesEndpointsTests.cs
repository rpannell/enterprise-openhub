using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using NSubstitute;
using OpenHub.RealtimeMessaging.Api.Managers.Models;
using OpenHub.RealtimeMessaging.Api.Models.Requests;
using OpenHub.RealtimeMessaging.Api.Tests.Integration.Fixtures;
using OpenHub.RealtimeMessaging.Contracts;
using Shouldly;
using Xunit;

namespace OpenHub.RealtimeMessaging.Api.Tests.Integration.Controllers;

[Trait("Category", "Integration")]
public class MessagesEndpointsTests : IClassFixture<RealtimeMessagingApiFactory>
{
    private readonly RealtimeMessagingApiFactory _factory;

    public MessagesEndpointsTests(RealtimeMessagingApiFactory factory)
    {
        _factory = factory;
    }

    private void AuthenticateAsTenantCaller(string realmSlug = "client-a")
        => _factory.ClaimsProvider.Claims =
        [
            new Claim("iss", $"http://keycloak.test/realms/{realmSlug}"),
            new Claim("scope", "realtime-messaging-publisher"),
        ];

    private void AuthenticateAsPlatformAdminCaller()
        => _factory.ClaimsProvider.Claims =
        [
            new Claim("iss", "http://keycloak.test/realms/platform-admin"),
            new Claim("scope", "realtime-messaging-publisher"),
        ];

    private static SendMessageRequest ValidTenantRequest(string tenantSlug = "client-a") => new()
    {
        Target = MessageTarget.Tenant,
        TenantSlug = tenantSlug,
        MessageType = "smoke-test",
        PayloadJson = "{}",
        CorrelationId = "corr-1",
    };

    [Fact]
    public async Task SendMessage_TenantCaller_Allowed_Returns202_AndPassesCallerRealm()
    {
        AuthenticateAsTenantCaller("client-a");
        _factory.MessageDispatchManagerMock
            .DispatchAsync(Arg.Any<CallerContextDto>(), Arg.Any<SendMessageDto>(), Arg.Any<CancellationToken>())
            .Returns(DispatchAuthorizationResultDto.Allow());

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/messages", ValidTenantRequest());

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await _factory.MessageDispatchManagerMock.Received(1).DispatchAsync(
            Arg.Is<CallerContextDto>(c => c.RealmSlug == "client-a" && !c.IsPlatformAdmin),
            Arg.Any<SendMessageDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMessage_PlatformAdminCaller_Allowed_Returns202_AndPassesIsPlatformAdmin()
    {
        AuthenticateAsPlatformAdminCaller();
        _factory.MessageDispatchManagerMock
            .DispatchAsync(Arg.Any<CallerContextDto>(), Arg.Any<SendMessageDto>(), Arg.Any<CancellationToken>())
            .Returns(DispatchAuthorizationResultDto.Allow());

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/messages", new SendMessageRequest
        {
            Target = MessageTarget.Broadcast,
            MessageType = "smoke-test",
            PayloadJson = "{}",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await _factory.MessageDispatchManagerMock.Received(1).DispatchAsync(
            Arg.Is<CallerContextDto>(c => c.IsPlatformAdmin),
            Arg.Any<SendMessageDto>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMessage_ManagerDenies_Returns403()
    {
        AuthenticateAsTenantCaller("client-a");
        _factory.MessageDispatchManagerMock
            .DispatchAsync(Arg.Any<CallerContextDto>(), Arg.Any<SendMessageDto>(), Arg.Any<CancellationToken>())
            .Returns(DispatchAuthorizationResultDto.Deny("not allowed"));

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/v1/messages", ValidTenantRequest("client-b"));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SendMessage_MissingPublisherScope_Returns403()
    {
        _factory.ClaimsProvider.Claims = [new Claim("iss", "http://keycloak.test/realms/client-a")];
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/messages", ValidTenantRequest());

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await _factory.MessageDispatchManagerMock.DidNotReceive().DispatchAsync(
            Arg.Any<CallerContextDto>(), Arg.Any<SendMessageDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendMessage_Unauthenticated_Returns401()
    {
        _factory.ClaimsProvider.Claims = null;
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/messages", ValidTenantRequest());

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SendMessage_MissingTenantSlugForTenantTarget_ReturnsBadRequestValidationProblem()
    {
        AuthenticateAsTenantCaller();
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/messages", new SendMessageRequest
        {
            Target = MessageTarget.Tenant,
            TenantSlug = null,
            MessageType = "smoke-test",
            PayloadJson = "{}",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
