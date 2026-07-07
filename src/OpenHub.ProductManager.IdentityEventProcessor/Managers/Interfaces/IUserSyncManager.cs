using OpenHub.Platform.IdentityEvents.Contracts;

namespace OpenHub.ProductManager.IdentityEventProcessor.Managers.Interfaces;

public interface IUserSyncManager
{
    Task HandleAsync(UserChangedEvent message, CancellationToken ct = default);
}
