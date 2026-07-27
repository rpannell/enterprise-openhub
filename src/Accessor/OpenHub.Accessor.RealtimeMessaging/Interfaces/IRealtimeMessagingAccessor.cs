namespace OpenHub.Accessor.RealtimeMessaging.Interfaces;

/// <summary>
/// Calls OpenHub.RealtimeMessaging.Api's NotificationHub directly as a SignalR client — there is
/// no REST surface on that service. Every method takes a pre-fetched M2M access token; callers
/// fetch it themselves via <c>OpenHub.Accessor.KeyCloak</c>'s
/// <c>IKeyCloakM2MTokenAccessor.GetTokenAsync(realmSlug)</c>, the same way
/// <c>ProductManagerApiAccessor</c> does today — this accessor takes no dependency on that
/// project and has no opinion about how a token gets fetched. <see cref="BroadcastAsync"/> only
/// succeeds server-side if the token's issuer is the platform-admin realm — this accessor
/// doesn't enforce that client-side, the Hub's declarative policy does.
/// </summary>
public interface IRealtimeMessagingAccessor
{
    Task SendToTenantAsync(
        string tenantSlug, string messageType, object payload, string accessToken,
        string? correlationId = null, CancellationToken ct = default);

    Task SendToTenantAsync(
        string tenantSlug, string messageType, string payloadJson, string accessToken,
        string? correlationId = null, CancellationToken ct = default);

    Task SendToUserAsync(
        string tenantSlug, string userId, string messageType, object payload, string accessToken,
        string? correlationId = null, CancellationToken ct = default);

    Task SendToUserAsync(
        string tenantSlug, string userId, string messageType, string payloadJson, string accessToken,
        string? correlationId = null, CancellationToken ct = default);

    Task BroadcastAsync(
        string messageType, object payload, string accessToken,
        string? correlationId = null, CancellationToken ct = default);

    Task BroadcastAsync(
        string messageType, string payloadJson, string accessToken,
        string? correlationId = null, CancellationToken ct = default);
}
