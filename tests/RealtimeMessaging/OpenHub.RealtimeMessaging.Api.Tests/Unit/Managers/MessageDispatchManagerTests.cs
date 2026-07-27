using NSubstitute;
using OpenHub.RealtimeMessaging.Api.Accessors.Interfaces;
using OpenHub.RealtimeMessaging.Api.Managers;
using OpenHub.RealtimeMessaging.Api.Managers.Engines.Interfaces;
using OpenHub.RealtimeMessaging.Api.Managers.Models;
using OpenHub.RealtimeMessaging.Contracts;
using Shouldly;
using Xunit;

namespace OpenHub.RealtimeMessaging.Api.Tests.Unit.Managers;

public class MessageDispatchManagerTests
{
    private static readonly CallerContextDto Caller = new("client-a", false);

    private static (MessageDispatchManager Manager, IMessageTargetAuthorizationEngine Engine, INotificationHubAccessor HubAccessor) CreateSut()
    {
        var engine = Substitute.For<IMessageTargetAuthorizationEngine>();
        var hubAccessor = Substitute.For<INotificationHubAccessor>();
        return (new MessageDispatchManager(engine, hubAccessor), engine, hubAccessor);
    }

    [Fact]
    public async Task SendToTenantAsync_EngineDenies_DoesNotCallAccessor()
    {
        var (manager, engine, hubAccessor) = CreateSut();
        engine.Authorize(Caller, "client-b").Returns(DispatchAuthorizationResultDto.Deny("no"));

        var result = await manager.SendToTenantAsync(Caller, "client-b", "test", "{}", null);

        result.Authorized.ShouldBeFalse();
        await hubAccessor.DidNotReceiveWithAnyArgs().SendToTenantGroupAsync(default!, default!);
    }

    [Fact]
    public async Task SendToTenantAsync_EngineAllows_SendsToTenantGroup()
    {
        var (manager, engine, hubAccessor) = CreateSut();
        engine.Authorize(Caller, "client-a").Returns(DispatchAuthorizationResultDto.Allow());

        var result = await manager.SendToTenantAsync(Caller, "client-a", "test", "{}", null);

        result.Authorized.ShouldBeTrue();
        await hubAccessor.Received(1).SendToTenantGroupAsync("client-a", Arg.Any<MessageEnvelope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendToUserAsync_EngineDenies_DoesNotCallAccessor()
    {
        var (manager, engine, hubAccessor) = CreateSut();
        engine.Authorize(Caller, "client-b").Returns(DispatchAuthorizationResultDto.Deny("no"));

        var result = await manager.SendToUserAsync(Caller, "client-b", "user-1", "test", "{}", null);

        result.Authorized.ShouldBeFalse();
        await hubAccessor.DidNotReceiveWithAnyArgs().SendToUserAsync(default!, default!, default!);
    }

    [Fact]
    public async Task SendToUserAsync_EngineAllows_SendsToUser()
    {
        var (manager, engine, hubAccessor) = CreateSut();
        engine.Authorize(Caller, "client-a").Returns(DispatchAuthorizationResultDto.Allow());

        await manager.SendToUserAsync(Caller, "client-a", "user-1", "test", "{}", null);

        await hubAccessor.Received(1).SendToUserAsync("client-a", "user-1", Arg.Any<MessageEnvelope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SendBroadcastAsync_SendsToAll_NoEngineCall()
    {
        var (manager, engine, hubAccessor) = CreateSut();

        await manager.SendBroadcastAsync("test", "{}", null);

        engine.DidNotReceiveWithAnyArgs().Authorize(default!, default!);
        await hubAccessor.Received(1).SendToAllAsync(Arg.Any<MessageEnvelope>(), Arg.Any<CancellationToken>());
    }
}
