using OpenHub.ProductManager.Contracts;

namespace OpenHub.ProductManager.Function.Managers.Interfaces;

public interface IProductSyncManager
{
    Task HandleAsync(ProductChangedEvent evt, CancellationToken ct = default);
}
