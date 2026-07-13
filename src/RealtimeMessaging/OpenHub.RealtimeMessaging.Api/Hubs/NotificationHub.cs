using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OpenHub.RealtimeMessaging.Api.Auth;
using OpenHub.RealtimeMessaging.Api.Managers.Interfaces;
using OpenHub.RealtimeMessaging.Api.Managers.Models;
using OpenHub.RealtimeMessaging.Contracts;

namespace OpenHub.RealtimeMessaging.Api.Hubs;

/// <summary>
/// Does all the work — both sides of real-time messaging live here, not split across a REST
/// controller and a hub. Front-end clients connect to receive; backend services and (eventually)
/// a not-yet-built UI connect and invoke the Send* methods below to publish.
/// </summary>
[Authorize]
public class NotificationHub(IMessageDispatchManager manager) : Hub<INotificationClient>
{
    public override async Task OnConnectedAsync()
    {
        var realmSlug = Context.User?.GetRealmSlug();

        if (!string.IsNullOrEmpty(realmSlug))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{realmSlug}");

        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Tenant path — any authenticated connection (M2M service or human user) may call this;
    /// the only gate is the runtime tenant-match check below (own realm, or platform-admin).
    /// </summary>
    public async Task SendToTenant(string tenantSlug, string messageType, string payloadJson, string? correlationId)
    {
        var caller = new CallerContextDto(Context.User?.GetRealmSlug(), Context.User?.IsPlatformAdmin() ?? false);
        var result = await manager.SendToTenantAsync(caller, tenantSlug, messageType, payloadJson, correlationId);
        if (!result.Authorized)
            throw new HubException(result.Reason);
    }

    /// <summary>Tenant path — same authorization shape as <see cref="SendToTenant"/>.</summary>
    public async Task SendToUser(string tenantSlug, string userId, string messageType, string payloadJson, string? correlationId)
    {
        var caller = new CallerContextDto(Context.User?.GetRealmSlug(), Context.User?.IsPlatformAdmin() ?? false);
        var result = await manager.SendToUserAsync(caller, tenantSlug, userId, messageType, payloadJson, correlationId);
        if (!result.Authorized)
            throw new HubException(result.Reason);
    }

    /// <summary>
    /// The OpenHub-admin path, not a tenant path — gated solely by the declarative policy below
    /// (caller's issuer is the platform-admin realm). No further runtime check is needed once
    /// the policy passes.
    /// </summary>
    [Authorize(Policy = "RealtimeMessagingBroadcastPublisher")]
    public Task Broadcast(string messageType, string payloadJson, string? correlationId)
        => manager.SendBroadcastAsync(messageType, payloadJson, correlationId);
}
