using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenHub.Accessor.ElasticIndex.Interfaces;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.ProductManager.Contracts;
using OpenHub.ProductManager.ReindexJob.Accessors.Interfaces;
using OpenHub.ProductManager.ReindexJob.Accessors.Models;
using OpenHub.ProductManager.ReindexJob.Managers.Engines.Interfaces;
using OpenHub.ProductManager.ReindexJob.Managers.Interfaces;
using OpenHub.ProductManager.ReindexJob.Managers.Models;
using OpenHub.ProductManager.ReindexJob.Options;

namespace OpenHub.ProductManager.ReindexJob.Managers;

public class ReindexManager(
    IElasticIndexAccessor<ProductDocument> indexAccessor,
    IKeyCloakM2MTokenAccessor m2mAccessor,
    IProductManagerApiAccessor apiAccessor,
    IReindexEngine engine,
    IOptions<ReindexJobOptions> options,
    ILogger<ReindexManager> logger) : IReindexManager
{
    // Must match the realm slug used when provisioning the platform-admin Keycloak client
    // and its KeyCloak:M2M:ClientSecrets entry — see AdminEndpoints.cs / product-manager
    // API's README "Platform-admin authentication" section.
    private const string PlatformAdminRealmSlug = "platform-admin";

    private readonly ReindexJobOptions _options = options.Value;

    public async Task<ReindexSummary> RunAsync(CancellationToken ct = default)
    {
        var newIndexName = await indexAccessor.CreateVersionedIndexAsync(ct);
        logger.LogInformation("Full reindex starting into {NewIndexName}.", newIndexName);

        var adminToken = await m2mAccessor.GetTokenAsync(PlatformAdminRealmSlug, ct);
        var tenants = await apiAccessor.GetTenantsAsync(adminToken, ct);
        logger.LogInformation("Reindexing {TenantCount} active tenant(s).", tenants.Count);

        var supersededIndices = new HashSet<string>();
        var failedTenantIds = new List<Guid>();
        var resultLock = new object();

        await Parallel.ForEachAsync(
            tenants,
            new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _options.TenantConcurrency), CancellationToken = ct },
            async (tenant, tenantCt) =>
            {
                try
                {
                    var previousIndices = await ReindexTenantAsync(tenant, newIndexName, tenantCt);
                    lock (resultLock)
                    {
                        foreach (var previous in previousIndices)
                            supersededIndices.Add(previous);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Reindex failed for tenant {TenantId} ({Slug}) — alias left on its previous index.",
                        tenant.Id, tenant.Slug);
                    lock (resultLock)
                    {
                        failedTenantIds.Add(tenant.Id);
                    }
                }
            });

        // Superseded-index cleanup is opportunistic, not required for correctness: a tenant's
        // alias is only ever moved once its own reindex succeeds, so a failure here just means
        // that tenant's alias (and whatever index it still points at) is untouched — safe to
        // leave and retry on the next scheduled run. Only clean up once nothing in this run
        // failed, so a chronically-failing tenant's index is never at risk of deletion.
        if (failedTenantIds.Count == 0)
        {
            foreach (var supersededIndex in supersededIndices)
                await indexAccessor.DeleteIndexIfUnreferencedAsync(supersededIndex, ct);
        }
        else
        {
            logger.LogWarning(
                "{FailedCount} of {TenantCount} tenant(s) failed this run — skipping cleanup of superseded indices.",
                failedTenantIds.Count, tenants.Count);
        }

        return new ReindexSummary { TenantCount = tenants.Count, FailedTenantIds = failedTenantIds };
    }

    /// <summary>Reindexes one tenant end to end and returns the index name(s) its alias previously pointed at.</summary>
    private async Task<IReadOnlyList<string>> ReindexTenantAsync(
        TenantSummaryApiResponse tenant, string newIndexName, CancellationToken ct)
    {
        var token = await m2mAccessor.GetTokenAsync(tenant.Slug, ct);

        var totalDocuments = 0;
        var failedDocuments = 0;
        var page = 1;

        while (true)
        {
            var pageResponse = await apiAccessor.GetProductsPageAsync(token, page, _options.PageSize, ct);
            if (pageResponse.Items.Count == 0)
                break;

            var documents = pageResponse.Items.Select(p => engine.BuildDocument(p, tenant.Id)).ToList();

            foreach (var chunk in documents.Chunk(_options.BatchSize))
            {
                var failedIds = await BulkIndexWithRetryAsync(newIndexName, chunk, ct);
                failedDocuments += failedIds.Count;
            }

            totalDocuments += documents.Count;

            if (page >= pageResponse.TotalPages)
                break;

            page++;
        }

        var failureRatePercent = totalDocuments == 0 ? 0 : (double)failedDocuments / totalDocuments * 100;
        if (failureRatePercent > _options.FailureThresholdPercent)
        {
            throw new InvalidOperationException(
                $"Tenant {tenant.Id} ({tenant.Slug}) had {failedDocuments}/{totalDocuments} " +
                $"({failureRatePercent:F1}%) document failures — exceeds the {_options.FailureThresholdPercent}% threshold.");
        }

        if (failedDocuments > 0)
        {
            logger.LogWarning(
                "Tenant {TenantId} ({Slug}) cutting over with {FailedCount}/{TotalCount} documents skipped ({FailureRate:F1}%).",
                tenant.Id, tenant.Slug, failedDocuments, totalDocuments, failureRatePercent);
        }

        await indexAccessor.EnsureTenantProvisionedAsync(tenant.Id, ct);
        var previousIndices = await indexAccessor.RepointTenantAliasAsync(tenant.Id, newIndexName, ct);

        logger.LogInformation(
            "Tenant {TenantId} ({Slug}) cut over to {NewIndexName} ({TotalCount} documents).",
            tenant.Id, tenant.Slug, newIndexName, totalDocuments);

        return previousIndices;
    }

    /// <summary>
    /// This job owns its own retry/backoff for bulk writes — unlike the trickle-feed consumer,
    /// it isn't running under Rebus, so there's no transport-level redelivery to lean on.
    /// </summary>
    private async Task<IReadOnlyList<Guid>> BulkIndexWithRetryAsync(
        string indexName, IReadOnlyList<ProductDocument> documents, CancellationToken ct)
    {
        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                return await indexAccessor.BulkIndexDocumentsAsync(indexName, documents, ct);
            }
            catch (Exception ex) when (attempt < _options.MaxBatchRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                logger.LogWarning(ex,
                    "Bulk index attempt {Attempt} of {MaxAttempts} into {IndexName} failed — retrying in {Delay}.",
                    attempt, _options.MaxBatchRetries, indexName, delay);
                await Task.Delay(delay, ct);
            }
        }
    }
}
