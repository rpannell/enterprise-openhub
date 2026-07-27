using OpenHub.RealtimeMessaging.Api.Managers.Models;

namespace OpenHub.RealtimeMessaging.Api.Managers.Interfaces;

public interface IMessageDispatchManager
{
    Task<DispatchAuthorizationResultDto> SendToTenantAsync(
        CallerContextDto caller, string tenantSlug, string messageType, string payloadJson,
        string? correlationId, CancellationToken ct = default);

    Task<DispatchAuthorizationResultDto> SendToUserAsync(
        CallerContextDto caller, string tenantSlug, string userId, string messageType, string payloadJson,
        string? correlationId, CancellationToken ct = default);

    Task SendBroadcastAsync(
        string messageType, string payloadJson, string? correlationId, CancellationToken ct = default);
}
