using OpenHub.ProductManager.Contracts;
using OpenHub.ProductManager.ReindexJob.Accessors.Models;

namespace OpenHub.ProductManager.ReindexJob.Managers.Engines.Interfaces;

public interface IReindexEngine
{
    /// <summary>Pure mapping from a fetched API product + owning tenant to the Elasticsearch document shape.</summary>
    ProductDocument BuildDocument(ProductApiResponse product, Guid tenantId);
}
