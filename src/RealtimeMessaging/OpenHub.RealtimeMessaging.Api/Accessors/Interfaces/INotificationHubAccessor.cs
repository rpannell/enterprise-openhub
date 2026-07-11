using OpenHub.RealtimeMessaging.Contracts;

namespace OpenHub.RealtimeMessaging.Api.Accessors.Interfaces;

public interface INotificationHubAccessor
{
    Task SendToGroupAsync(string group, MessageEnvelope envelope, CancellationToken ct = default);

    Task SendToAllAsync(MessageEnvelope envelope, CancellationToken ct = default);
}
