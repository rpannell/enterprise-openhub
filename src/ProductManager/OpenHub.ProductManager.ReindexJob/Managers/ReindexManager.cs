using System.Net;
using System.Net.Http;
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

    // A 401 here almost always means the M2M token cached for this realm slug has expired —
    // the cache's fixed TTL (see KeyCloakM2MTokenAccessor.TokenCacheOptions) is a conservative
    // guess, not tied to the real Keycloak-issued expiry, and a single tenant's reindex can run
    // long enough to outlive it. Force a fresh token and retry before giving up.
    private const int MaxTokenRetryAttempts = 3;

    private readonly ReindexJobOptions _options = options.Value;

    public async Task<ReindexSummary> RunAsync(CancellationToken ct = default)
    {
        var runStartTime = DateTime.UtcNow;
        var newIndexName = await indexAccessor.CreateVersionedIndexAsync(ct);
        logger.LogInformation("Full reindex starting into {NewIndexName}.", newIndexName);

        var tenants = await CallWithFreshTokenRetryAsync(
            PlatformAdminRealmSlug, (token, callCt) => apiAccessor.GetTenantsAsync(token, callCt), ct);
        logger.LogInformation("Reindexing {TenantCount} active tenant(s). Max concurrency: {MaxConcurrency}.", 
            tenants.Count, Math.Max(1, _options.TenantConcurrency));

        var supersededIndices = new HashSet<string>();
        var failedTenantIds = new List<Guid>();
        var resultLock = new object();
        var completedTenants = 0;

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
                        completedTenants++;
                        foreach (var previous in previousIndices)
                            supersededIndices.Add(previous);
                        logger.LogInformation("Tenant reindex progress: {CompletedCount}/{TotalCount} completed.", 
                            completedTenants, tenants.Count);
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
            logger.LogInformation("Cleaning up superseded indices.");
            foreach (var supersededIndex in supersededIndices)
                await indexAccessor.DeleteIndexIfUnreferencedAsync(supersededIndex, ct);
        }
        else
        {
            logger.LogWarning(
                "{FailedCount} of {TenantCount} tenant(s) failed this run — skipping cleanup of superseded indices.",
                failedTenantIds.Count, tenants.Count);
        }

        var totalElapsed = DateTime.UtcNow - runStartTime;
        logger.LogInformation("Full reindex completed in {Elapsed:F1}s. Success: {SuccessCount}/{TotalCount} tenants.", 
            totalElapsed.TotalSeconds, tenants.Count - failedTenantIds.Count, tenants.Count);

        return new ReindexSummary { TenantCount = tenants.Count, FailedTenantIds = failedTenantIds };
    }

    /// <summary>Reindexes one tenant end to end and returns the index name(s) its alias previously pointed at.</summary>
    private async Task<IReadOnlyList<string>> ReindexTenantAsync(
        TenantSummaryApiResponse tenant, string newIndexName, CancellationToken ct)
    {
        logger.LogInformation("Starting reindex for tenant {TenantId} ({Slug}).", tenant.Id, tenant.Slug);

        var totalDocuments = 0;
        var failedDocuments = 0;
        var progressLock = new object();
        var pageStartTime = DateTime.UtcNow;

        // Page 1 first, sequentially — it tells us TotalPages, which every other page needs to
        // even exist as a unit of work. Pages are otherwise independent (offset-based, writing
        // into a brand-new index nothing else reads yet), so 2..TotalPages fan out freely.
        var firstPage = await FetchProductsPageAsync(tenant, 1, ct);
        await ProcessPageAsync(tenant, newIndexName, firstPage, RecordProgress, ct);

        if (firstPage.TotalPages > 1)
        {
            var remainingPages = Enumerable.Range(2, firstPage.TotalPages - 1);
            await Parallel.ForEachAsync(
                remainingPages,
                new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, _options.PageConcurrency), CancellationToken = ct },
                async (page, pageCt) =>
                {
                    var pageResponse = await FetchProductsPageAsync(tenant, page, pageCt);
                    await ProcessPageAsync(tenant, newIndexName, pageResponse, RecordProgress, pageCt);
                });
        }

        void RecordProgress(int documentCount, int failedCount)
        {
            lock (progressLock)
            {
                totalDocuments += documentCount;
                failedDocuments += failedCount;
                logger.LogInformation(
                    "Tenant {TenantId} ({Slug}) progress: {TotalDocuments} documents indexed so far in {Elapsed:F1}s.",
                    tenant.Id, tenant.Slug, totalDocuments, (DateTime.UtcNow - pageStartTime).TotalSeconds);
            }
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
            "Tenant {TenantId} ({Slug}) cut over to {NewIndexName} ({TotalCount} documents) in {Elapsed:F1}s.",
            tenant.Id, tenant.Slug, newIndexName, totalDocuments, (DateTime.UtcNow - pageStartTime).TotalSeconds);

        return previousIndices;
    }

    private Task<PagedApiResponse<ProductApiResponse>> FetchProductsPageAsync(
        TenantSummaryApiResponse tenant, int page, CancellationToken ct)
    {
        logger.LogDebug("Fetching products page {Page} for tenant {TenantId} ({Slug}).", page, tenant.Id, tenant.Slug);
        return CallWithFreshTokenRetryAsync(
            tenant.Slug, (token, callCt) => apiAccessor.GetProductsPageAsync(token, page, _options.PageSize, callCt), ct);
    }

    /// <summary>Builds documents for one already-fetched page and bulk-indexes them, reporting
    /// (documentCount, failedCount) back to the caller for thread-safe accumulation.</summary>
    private async Task ProcessPageAsync(
        TenantSummaryApiResponse tenant, string newIndexName, PagedApiResponse<ProductApiResponse> pageResponse,
        Action<int, int> recordProgress, CancellationToken ct)
    {
        if (pageResponse.Items.Count == 0)
        {
            recordProgress(0, 0);
            return;
        }

        var documents = pageResponse.Items.Select(p => engine.BuildDocument(p, tenant.Id)).ToList();
        var failedCount = 0;

        foreach (var chunk in documents.Chunk(_options.BatchSize))
        {
            logger.LogDebug("Submitting batch of {BatchSize} documents to index {IndexName} for tenant {TenantId}.",
                chunk.Length, newIndexName, tenant.Id);
            var failedIds = await BulkIndexWithRetryAsync(newIndexName, chunk, ct);
            if (failedIds.Count > 0)
            {
                logger.LogWarning(
                    "Batch submission for tenant {TenantId} ({Slug}) had {FailedCount}/{BatchSize} document failures.",
                    tenant.Id, tenant.Slug, failedIds.Count, chunk.Length);
                failedCount += failedIds.Count;
            }
        }

        recordProgress(documents.Count, failedCount);
    }

    /// <summary>
    /// Calls apiCall with a token for realmSlug, retrying up to MaxTokenRetryAttempts times if it
    /// comes back 401 — each retry forces a fresh token via InvalidateTokenAsync first, since a
    /// 401 means the cached one is no longer good regardless of what its remaining cached TTL
    /// says. Any other failure (or a 401 on the final attempt) propagates as-is.
    /// </summary>
    private async Task<T> CallWithFreshTokenRetryAsync<T>(
        string realmSlug, Func<string, CancellationToken, Task<T>> apiCall, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            var token = await m2mAccessor.GetTokenAsync(realmSlug, ct);
            try
            {
                return await apiCall(token, ct);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                if (attempt >= MaxTokenRetryAttempts)
                {
                    logger.LogError(ex,
                        "Got 401 from realm '{RealmSlug}' after {Attempts} attempts — giving up.",
                        realmSlug, attempt);
                    throw;
                }

                logger.LogWarning(
                    "Got 401 from realm '{RealmSlug}' (attempt {Attempt}/{MaxAttempts}) — token likely expired, fetching a fresh one and retrying.",
                    realmSlug, attempt, MaxTokenRetryAttempts);
                await m2mAccessor.InvalidateTokenAsync(realmSlug, ct);
            }
        }
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
                var failedIds = await indexAccessor.BulkIndexDocumentsAsync(indexName, documents, ct);
                if (failedIds.Count == 0)
                {
                    logger.LogDebug("Successfully indexed batch of {DocumentCount} documents into {IndexName}.", 
                        documents.Count, indexName);
                }
                return failedIds;
            }
            catch (Exception ex) when (attempt < _options.MaxBatchRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                logger.LogWarning(ex,
                    "Bulk index attempt {Attempt} of {MaxAttempts} into {IndexName} failed — retrying in {Delay}s.",
                    attempt, _options.MaxBatchRetries, indexName, delay.TotalSeconds);
                await Task.Delay(delay, ct);
            }
        }
    }
}
