using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OpenHub.RealtimeMessaging.Api.Auth;
using OpenHub.RealtimeMessaging.Contracts;

namespace OpenHub.RealtimeMessaging.Api.Hubs;

/// <summary>
/// Receive-side only — front-end clients connect here to get pushed messages. Group membership
/// is derived from the connecting user's own JWT claims; there is no client-invokable method,
/// since messages only ever flow server -&gt; client (see Controllers/MessagesEndpoints.cs for the
/// M2M-authenticated publish side).
/// </summary>
[Authorize]
public class NotificationHub : Hub<INotificationClient>
{
    public override async Task OnConnectedAsync()
    {
        var realmSlug = Context.User?.GetRealmSlug();
        var userId = Context.User?.FindFirst("sub")?.Value;

        if (!string.IsNullOrEmpty(realmSlug))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"tenant:{realmSlug}");

        if (!string.IsNullOrEmpty(realmSlug) && !string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{realmSlug}:{userId}");

        await base.OnConnectedAsync();
    }
}
