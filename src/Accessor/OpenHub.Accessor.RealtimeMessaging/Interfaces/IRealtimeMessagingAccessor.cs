namespace OpenHub.Accessor.RealtimeMessaging.Interfaces;

/// <summary>
/// Calls OpenHub.RealtimeMessaging.Api's publish endpoint. Every method takes a pre-fetched M2M
/// access token — callers fetch it themselves via
/// <c>OpenHub.Accessor.KeyCloak</c>'s <c>IKeyCloakM2MTokenAccessor.GetTokenAsync(realmSlug)</c>,
/// the same way <c>ProductManagerApiAccessor</c> does today. <see cref="SendToAllAsync"/> only
/// succeeds server-side if the token was fetched for the "platform-admin" realm — this accessor
/// doesn't enforce that client-side, OpenHub.RealtimeMessaging.Api's authorization Engine does.
/// </summary>
public interface IRealtimeMessagingAccessor
{
    Task SendToTenantAsync(
        string tenantSlug, string messageType, string payloadJson, string accessToken,
        string? correlationId = null, CancellationToken ct = default);

    Task SendToUserAsync(
        string tenantSlug, string userId, string messageType, string payloadJson, string accessToken,
        string? correlationId = null, CancellationToken ct = default);

    Task SendToAllAsync(
        string messageType, string payloadJson, string accessToken,
        string? correlationId = null, CancellationToken ct = default);
}
