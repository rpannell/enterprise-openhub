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
    public async Task<DispatchAuthorizationResultDto> SendToTenantAsync(
        CallerContextDto caller, string tenantSlug, string messageType, string payloadJson,
        string? correlationId, CancellationToken ct = default)
    {
        var result = engine.Authorize(caller, tenantSlug);
        if (!result.Authorized)
            return result;

        var envelope = new MessageEnvelope(messageType, payloadJson, correlationId, DateTimeOffset.UtcNow);
        await hubAccessor.SendToTenantGroupAsync(tenantSlug, envelope, ct);
        return result;
    }

    public async Task<DispatchAuthorizationResultDto> SendToUserAsync(
        CallerContextDto caller, string tenantSlug, string userId, string messageType, string payloadJson,
        string? correlationId, CancellationToken ct = default)
    {
        var result = engine.Authorize(caller, tenantSlug);
        if (!result.Authorized)
            return result;

        var envelope = new MessageEnvelope(messageType, payloadJson, correlationId, DateTimeOffset.UtcNow);
        await hubAccessor.SendToUserAsync(tenantSlug, userId, envelope, ct);
        return result;
    }

    public async Task SendBroadcastAsync(
        string messageType, string payloadJson, string? correlationId, CancellationToken ct = default)
    {
        var envelope = new MessageEnvelope(messageType, payloadJson, correlationId, DateTimeOffset.UtcNow);
        await hubAccessor.SendToAllAsync(envelope, ct);
    }
}
