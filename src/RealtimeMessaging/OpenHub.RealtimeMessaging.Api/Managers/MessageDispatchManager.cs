using OpenHub.RealtimeMessaging.Api.Accessors.Interfaces;
using OpenHub.RealtimeMessaging.Api.Managers.Engines.Interfaces;
using OpenHub.RealtimeMessaging.Api.Managers.Interfaces;
using OpenHub.RealtimeMessaging.Api.Managers.Models;
using OpenHub.RealtimeMessaging.Contracts;

namespace OpenHub.RealtimeMessaging.Api.Managers;

public class MessageDispatchManager(
    IMessageTargetAuthorizationEngine engine,
    INotificationHubAccessor hubAccessor) : IMessageDispatchManager
{
    public async Task<DispatchAuthorizationResultDto> DispatchAsync(
        CallerContextDto caller, SendMessageDto message, CancellationToken ct = default)
    {
        var authResult = engine.Authorize(caller, message);
        if (!authResult.Authorized)
            return authResult;

        var envelope = new MessageEnvelope(
            message.MessageType, message.PayloadJson, message.CorrelationId, DateTimeOffset.UtcNow);

        switch (message.Target)
        {
            case MessageTarget.User:
                await hubAccessor.SendToGroupAsync($"user:{message.TenantSlug}:{message.UserId}", envelope, ct);
                break;
            case MessageTarget.Tenant:
                await hubAccessor.SendToGroupAsync($"tenant:{message.TenantSlug}", envelope, ct);
                break;
            case MessageTarget.Broadcast:
                await hubAccessor.SendToAllAsync(envelope, ct);
                break;
        }

        return authResult;
    }
}
