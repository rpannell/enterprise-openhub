using OpenHub.ProductManager.Contracts;
using OpenHub.ProductManager.ElasticProcessor.Accessors.Models;

namespace OpenHub.ProductManager.ElasticProcessor.Managers.Engines.Interfaces;

public interface IProductSyncEngine
{
    /// <summary>Pure decision of whether an event needs a downstream fetch, based only on its own fields.</summary>
    bool ShouldFetchProduct(ProductChangedEvent evt);

    /// <summary>Pure mapping from the fetched API product + originating event to the Elasticsearch document shape.</summary>
    ProductDocument BuildDocument(ProductApiResponse product, ProductChangedEvent evt);
}
