namespace OpenHub.ProductManager.ReindexJob.Options;

public class ReindexJobOptions
{
    public const string SectionName = "ReindexJob";

    /// <summary>Rows requested per GET /api/v1/products page call.</summary>
    public int PageSize { get; set; } = 200;

    /// <summary>Documents per Elasticsearch bulk request.</summary>
    public int BatchSize { get; set; } = 500;

    /// <summary>Retries for a single bulk-index batch before giving up on that batch.</summary>
    public int MaxBatchRetries { get; set; } = 3;

    /// <summary>How many tenants to reindex concurrently.</summary>
    public int TenantConcurrency { get; set; } = 2;

    /// <summary>
    /// Max share of a tenant's documents allowed to fail (after retries) before that
    /// tenant's whole run is failed for this cycle instead of cutting over with gaps.
    /// </summary>
    public double FailureThresholdPercent { get; set; } = 5.0;
}
