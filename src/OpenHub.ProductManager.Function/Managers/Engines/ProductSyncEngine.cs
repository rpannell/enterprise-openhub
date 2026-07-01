using OpenHub.ProductManager.Contracts;
using OpenHub.ProductManager.Function.Managers.Engines.Interfaces;

namespace OpenHub.ProductManager.Function.Managers.Engines;

public class ProductSyncEngine : IProductSyncEngine
{
    // ProductManager.Api's GET /products/{id} 404s for soft-deleted products,
    // so a delete event has nothing to fetch — the deletion itself is the payload.
    public bool ShouldFetchProduct(ProductChangedEvent evt) => !evt.IsDeleted;
}
