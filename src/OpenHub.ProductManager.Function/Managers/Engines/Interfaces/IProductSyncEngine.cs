using OpenHub.ProductManager.Contracts;

namespace OpenHub.ProductManager.Function.Managers.Engines.Interfaces;

public interface IProductSyncEngine
{
    /// <summary>Pure decision of whether an event needs a downstream fetch, based only on its own fields.</summary>
    bool ShouldFetchProduct(ProductChangedEvent evt);
}
