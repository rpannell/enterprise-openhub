using Microsoft.Extensions.Logging;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.ProductManager.Contracts;
using OpenHub.ProductManager.ElasticProcessor.Accessors.Interfaces;
using OpenHub.ProductManager.ElasticProcessor.Managers.Engines.Interfaces;
using OpenHub.ProductManager.ElasticProcessor.Managers.Interfaces;

namespace OpenHub.ProductManager.ElasticProcessor.Managers;

public class ProductSyncManager(
    IKeyCloakM2MTokenAccessor m2mAccessor,
    IProductManagerApiAccessor productApiAccessor,
    IProductSyncEngine engine,
    ILogger<ProductSyncManager> logger) : IProductSyncManager
{
    public async Task HandleAsync(ProductChangedEvent evt, CancellationToken ct = default)
    {
        if (!engine.ShouldFetchProduct(evt))
        {
            logger.LogInformation(
                "Product {ProductId} (tenant {TenantId}) was deleted — skipping fetch.",
                evt.ProductId, evt.TenantId);
            return;
        }

        var token = await m2mAccessor.GetTokenAsync(evt.RealmSlug, ct);
        var product = await productApiAccessor.GetProductByIdAsync(evt.ProductId, token, ct);

        if (product is null)
        {
            logger.LogWarning(
                "Product {ProductId} (tenant {TenantId}, realm {RealmSlug}) not found via API.",
                evt.ProductId, evt.TenantId, evt.RealmSlug);
            return;
        }

        // TODO: write to Elastic — deferred until the indexing design is finalized.
        logger.LogInformation(
            "Fetched product {ProductId} ('{Sku}') for tenant {TenantId}.",
            product.Id, product.Sku, evt.TenantId);
    }
}
