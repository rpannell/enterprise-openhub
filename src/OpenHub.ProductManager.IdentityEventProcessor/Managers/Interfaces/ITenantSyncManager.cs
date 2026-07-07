using OpenHub.Platform.IdentityEvents.Contracts;

namespace OpenHub.ProductManager.IdentityEventProcessor.Managers.Interfaces;

public interface ITenantSyncManager
{
    Task HandleAsync(TenantChangedEvent message, CancellationToken ct = default);
}
