using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using NSubstitute;
using OpenHub.RealtimeMessaging.Api.Hubs;
using OpenHub.RealtimeMessaging.Api.Managers.Interfaces;
using OpenHub.RealtimeMessaging.Api.Managers.Models;
using Shouldly;
using Xunit;

namespace OpenHub.RealtimeMessaging.Api.Tests.Unit.Hubs;

/// <summary>
/// Exercises NotificationHub's method bodies directly — Context/Groups are public settable on
/// Hub&lt;T&gt; specifically to support this, no WebApplicationFactory needed. This does NOT
/// exercise the [Authorize(Policy=...)] attribute on Broadcast — that's ASP.NET Core
/// authorization middleware, evaluated before the method body ever runs, and is covered instead
/// by a real-connection integration test.
/// </summary>
public class NotificationHubTests
{
    private const string ConnectionId = "conn-1";

    private static ClaimsPrincipal TenantUser(string realmSlug = "client-a", string sub = "user-1") => new(
        new ClaimsIdentity([
            new Claim("iss", $"http://keycloak.test/realms/{realmSlug}"),
            new Claim("sub", sub),
        ], "Test"));

    private static (NotificationHub Hub, IMessageDispatchManager Manager, IGroupManager Groups) CreateSut(ClaimsPrincipal? user = null)
    {
        var manager = Substitute.For<IMessageDispatchManager>();
        var hub = new NotificationHub(manager);

        var context = Substitute.For<HubCallerContext>();
        context.ConnectionId.Returns(ConnectionId);
        context.User.Returns(user ?? TenantUser());
        hub.Context = context;

        var groups = Substitute.For<IGroupManager>();
        hub.Groups = groups;

        return (hub, manager, groups);
    }

    [Fact]
    public async Task OnConnectedAsync_ResolvableRealm_JoinsTenantGroup()
    {
        var (hub, _, groups) = CreateSut(TenantUser("client-a"));

        await hub.OnConnectedAsync();

        await groups.Received(1).AddToGroupAsync(ConnectionId, "tenant:client-a", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnConnectedAsync_NoResolvableRealm_DoesNotJoinGroup()
    {
        var anonymousUser = new ClaimsPrincipal(new ClaimsIdentity());
        var (hub, _, groups) = CreateSut(anonymousUser);

        await hub.OnConnectedAsync();

        await groups.DidNotReceiveWithAnyArgs().AddToGroupAsync(default!, default!, default);
    }

    [Fact]
    public async Task SendToTenant_ManagerAllows_CompletesWithoutThrowing()
    {
        var (hub, manager, _) = CreateSut();
        manager.SendToTenantAsync(Arg.Any<CallerContextDto>(), "client-a", "test", "{}", null, Arg.Any<CancellationToken>())
            .Returns(DispatchAuthorizationResultDto.Allow());

        await Should.NotThrowAsync(() => hub.SendToTenant("client-a", "test", "{}", null));
    }

    [Fact]
    public async Task SendToTenant_ManagerDenies_ThrowsHubException()
    {
        var (hub, manager, _) = CreateSut();
        manager.SendToTenantAsync(Arg.Any<CallerContextDto>(), "client-b", "test", "{}", null, Arg.Any<CancellationToken>())
            .Returns(DispatchAuthorizationResultDto.Deny("not your tenant"));

        var ex = await Should.ThrowAsync<HubException>(() => hub.SendToTenant("client-b", "test", "{}", null));
        ex.Message.ShouldContain("not your tenant");
    }

    [Fact]
    public async Task SendToUser_ManagerDenies_ThrowsHubException()
    {
        var (hub, manager, _) = CreateSut();
        manager.SendToUserAsync(Arg.Any<CallerContextDto>(), "client-b", "user-2", "test", "{}", null, Arg.Any<CancellationToken>())
            .Returns(DispatchAuthorizationResultDto.Deny("not your tenant"));

        await Should.ThrowAsync<HubException>(() => hub.SendToUser("client-b", "user-2", "test", "{}", null));
    }

    [Fact]
    public async Task Broadcast_DelegatesToManager()
    {
        var (hub, manager, _) = CreateSut();

        await hub.Broadcast("test", "{}", "corr-1");

        await manager.Received(1).SendBroadcastAsync("test", "{}", "corr-1", Arg.Any<CancellationToken>());
    }
}
