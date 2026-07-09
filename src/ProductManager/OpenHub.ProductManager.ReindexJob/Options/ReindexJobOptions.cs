namespace OpenHub.ProductManager.ReindexJob.Options;

public class ReindexJobOptions
{
    public const string SectionName = "ReindexJob";

    /// <summary>Rows requested per GET /api/v1/products page call. Must stay within
    /// ProductListRequestValidator's InclusiveBetween(1, 100) on the API side.</summary>
    public int PageSize { get; set; } = 100;

    /// <summary>Documents per Elasticsearch bulk request.</summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>Retries for a single bulk-index batch before giving up on that batch.</summary>
    public int MaxBatchRetries { get; set; } = 3;

    /// <summary>How many tenants to reindex concurrently.</summary>
    public int TenantConcurrency { get; set; } = 4;

    /// <summary>How many pages to fetch/index concurrently within a single tenant. Safe because
    /// each page is independent (offset-based) and writes go into a brand-new index no other
    /// reader sees until cutover, so there's no ordering to preserve.</summary>
    public int PageConcurrency { get; set; } = 4;

    /// <summary>
    /// Max share of a tenant's documents allowed to fail (after retries) before that
    /// tenant's whole run is failed for this cycle instead of cutting over with gaps.
    /// </summary>
    public double FailureThresholdPercent { get; set; } = 5.0;
}
