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
    /// <summary>Serializes <paramref name="payload"/> with System.Text.Json (camelCase, the
    /// ASP.NET Core Web defaults) before sending — use this unless you already have a JSON
    /// string in hand, in which case call the <c>payloadJson</c> overload directly.</summary>
    Task SendToTenantAsync(
        string tenantSlug, string messageType, object payload, string accessToken,
        string? correlationId = null, CancellationToken ct = default);

    Task SendToTenantAsync(
        string tenantSlug, string messageType, string payloadJson, string accessToken,
        string? correlationId = null, CancellationToken ct = default);

    /// <summary>Serializes <paramref name="payload"/> with System.Text.Json (camelCase, the
    /// ASP.NET Core Web defaults) before sending — use this unless you already have a JSON
    /// string in hand, in which case call the <c>payloadJson</c> overload directly.</summary>
    Task SendToUserAsync(
        string tenantSlug, string userId, string messageType, object payload, string accessToken,
        string? correlationId = null, CancellationToken ct = default);

    Task SendToUserAsync(
        string tenantSlug, string userId, string messageType, string payloadJson, string accessToken,
        string? correlationId = null, CancellationToken ct = default);

    /// <summary>Serializes <paramref name="payload"/> with System.Text.Json (camelCase, the
    /// ASP.NET Core Web defaults) before sending — use this unless you already have a JSON
    /// string in hand, in which case call the <c>payloadJson</c> overload directly.</summary>
    Task SendToAllAsync(
        string messageType, object payload, string accessToken,
        string? correlationId = null, CancellationToken ct = default);

    Task SendToAllAsync(
        string messageType, string payloadJson, string accessToken,
        string? correlationId = null, CancellationToken ct = default);
}
