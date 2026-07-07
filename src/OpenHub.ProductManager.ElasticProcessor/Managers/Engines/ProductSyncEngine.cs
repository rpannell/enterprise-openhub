using OpenHub.ProductManager.Contracts;
using OpenHub.ProductManager.ElasticProcessor.Accessors.Models;
using OpenHub.ProductManager.ElasticProcessor.Managers.Engines.Interfaces;

namespace OpenHub.ProductManager.ElasticProcessor.Managers.Engines;

public class ProductSyncEngine : IProductSyncEngine
{
    // ProductManager.Api's GET /products/{id} 404s for soft-deleted products,
    // so a delete event has nothing to fetch — the deletion itself is the payload.
    public bool ShouldFetchProduct(ProductChangedEvent evt) => !evt.IsDeleted;

    public ProductDocument BuildDocument(ProductApiResponse product, ProductChangedEvent evt) => new()
    {
        Id = product.Id,
        TenantId = evt.TenantId,
        Sku = product.Sku,
        Name = product.Name,
        Description = product.Description,
        IsActive = product.IsActive,
        IsPublished = product.IsPublished,
    };
}
