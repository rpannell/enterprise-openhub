using OpenHub.RealtimeMessaging.Contracts;

namespace OpenHub.RealtimeMessaging.Api.Accessors.Interfaces;

public interface INotificationHubAccessor
{
    Task SendToTenantGroupAsync(string tenantSlug, MessageEnvelope envelope, CancellationToken ct = default);

    Task SendToUserAsync(string realmSlug, string userId, MessageEnvelope envelope, CancellationToken ct = default);

    Task SendToAllAsync(MessageEnvelope envelope, CancellationToken ct = default);
}
