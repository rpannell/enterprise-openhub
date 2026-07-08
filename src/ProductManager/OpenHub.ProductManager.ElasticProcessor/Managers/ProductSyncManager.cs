using Microsoft.Extensions.Logging;
using OpenHub.Accessor.ElasticIndex.Interfaces;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.ProductManager.Contracts;
using OpenHub.ProductManager.ElasticProcessor.Accessors.Interfaces;
using OpenHub.ProductManager.ElasticProcessor.Managers.Engines.Interfaces;
using OpenHub.ProductManager.ElasticProcessor.Managers.Interfaces;

namespace OpenHub.ProductManager.ElasticProcessor.Managers;

public class ProductSyncManager(
    IKeyCloakM2MTokenAccessor m2mAccessor,
    IProductManagerApiAccessor productApiAccessor,
    IElasticIndexAccessor<ProductDocument> productIndexAccessor,
    IProductSyncEngine engine,
    ILogger<ProductSyncManager> logger) : IProductSyncManager
{
    public async Task HandleAsync(ProductChangedEvent evt, CancellationToken ct = default)
    {
        if (!engine.ShouldFetchProduct(evt))
        {
            await productIndexAccessor.DeleteDocumentAsync(evt.TenantId, evt.ProductId, ct);
            logger.LogInformation(
                "Product {ProductId} (tenant {TenantId}) was deleted — removed from Elasticsearch.",
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

        var document = engine.BuildDocument(product, evt);

        await productIndexAccessor.EnsureTenantProvisionedAsync(evt.TenantId, ct);
        await productIndexAccessor.IndexDocumentAsync(document, ct);

        logger.LogInformation(
            "Indexed product {ProductId} ('{Sku}') for tenant {TenantId}.",
            product.Id, product.Sku, evt.TenantId);
    }
}
