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

    [Fact]
    public async Task DispatchAsync_EngineDenies_DoesNotCallAccessor()
    {
        var engine = Substitute.For<IMessageTargetAuthorizationEngine>();
        var hubAccessor = Substitute.For<INotificationHubAccessor>();
        var denyResult = DispatchAuthorizationResultDto.Deny("no");
        engine.Authorize(Arg.Any<CallerContextDto>(), Arg.Any<SendMessageDto>()).Returns(denyResult);
        var manager = new MessageDispatchManager(engine, hubAccessor);
        var message = new SendMessageDto(MessageTarget.Tenant, "client-b", null, "test", "{}", null);

        var result = await manager.DispatchAsync(Caller, message);

        result.Authorized.ShouldBeFalse();
        await hubAccessor.DidNotReceiveWithAnyArgs().SendToGroupAsync(default!, default!);
        await hubAccessor.DidNotReceiveWithAnyArgs().SendToAllAsync(default!);
    }

    [Fact]
    public async Task DispatchAsync_TenantTarget_Allowed_SendsToTenantGroup()
    {
        var engine = Substitute.For<IMessageTargetAuthorizationEngine>();
        var hubAccessor = Substitute.For<INotificationHubAccessor>();
        engine.Authorize(Arg.Any<CallerContextDto>(), Arg.Any<SendMessageDto>())
            .Returns(DispatchAuthorizationResultDto.Allow());
        var manager = new MessageDispatchManager(engine, hubAccessor);
        var message = new SendMessageDto(MessageTarget.Tenant, "client-a", null, "test", "{}", null);

        var result = await manager.DispatchAsync(Caller, message);

        result.Authorized.ShouldBeTrue();
        await hubAccessor.Received(1).SendToGroupAsync("tenant:client-a", Arg.Any<MessageEnvelope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_UserTarget_Allowed_SendsToUserGroup()
    {
        var engine = Substitute.For<IMessageTargetAuthorizationEngine>();
        var hubAccessor = Substitute.For<INotificationHubAccessor>();
        engine.Authorize(Arg.Any<CallerContextDto>(), Arg.Any<SendMessageDto>())
            .Returns(DispatchAuthorizationResultDto.Allow());
        var manager = new MessageDispatchManager(engine, hubAccessor);
        var message = new SendMessageDto(MessageTarget.User, "client-a", "user-1", "test", "{}", null);

        await manager.DispatchAsync(Caller, message);

        await hubAccessor.Received(1).SendToGroupAsync("user:client-a:user-1", Arg.Any<MessageEnvelope>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchAsync_BroadcastTarget_Allowed_SendsToAll()
    {
        var engine = Substitute.For<IMessageTargetAuthorizationEngine>();
        var hubAccessor = Substitute.For<INotificationHubAccessor>();
        engine.Authorize(Arg.Any<CallerContextDto>(), Arg.Any<SendMessageDto>())
            .Returns(DispatchAuthorizationResultDto.Allow());
        var manager = new MessageDispatchManager(engine, hubAccessor);
        var message = new SendMessageDto(MessageTarget.Broadcast, null, null, "test", "{}", null);

        await manager.DispatchAsync(new CallerContextDto("platform-admin", true), message);

        await hubAccessor.Received(1).SendToAllAsync(Arg.Any<MessageEnvelope>(), Arg.Any<CancellationToken>());
    }
}
