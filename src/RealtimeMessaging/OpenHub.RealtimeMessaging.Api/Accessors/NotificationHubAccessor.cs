using Microsoft.AspNetCore.SignalR;
using OpenHub.RealtimeMessaging.Api.Accessors.Interfaces;
using OpenHub.RealtimeMessaging.Api.Hubs;
using OpenHub.RealtimeMessaging.Contracts;

namespace OpenHub.RealtimeMessaging.Api.Accessors;

/// <summary>The only place <see cref="IHubContext{THub, T}"/> is injected — everything else
/// talks to the hub through <see cref="INotificationHubAccessor"/>.</summary>
public class NotificationHubAccessor(IHubContext<NotificationHub, INotificationClient> hubContext)
    : INotificationHubAccessor
{
    public Task SendToGroupAsync(string group, MessageEnvelope envelope, CancellationToken ct = default) =>
        hubContext.Clients.Group(group).ReceiveMessage(envelope);

    public Task SendToAllAsync(MessageEnvelope envelope, CancellationToken ct = default) =>
        hubContext.Clients.All.ReceiveMessage(envelope);
}
