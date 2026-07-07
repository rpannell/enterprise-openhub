using OpenHub.ProductManager.Contracts;
using OpenHub.ProductManager.ReindexJob.Accessors.Models;
using OpenHub.ProductManager.ReindexJob.Managers.Engines.Interfaces;

namespace OpenHub.ProductManager.ReindexJob.Managers.Engines;

public class ReindexEngine : IReindexEngine
{
    public ProductDocument BuildDocument(ProductApiResponse product, Guid tenantId) => new()
    {
        Id = product.Id,
        TenantId = tenantId,
        Sku = product.Sku,
        Name = product.Name,
        Description = product.Description,
        IsActive = product.IsActive,
        IsPublished = product.IsPublished,
    };
}
