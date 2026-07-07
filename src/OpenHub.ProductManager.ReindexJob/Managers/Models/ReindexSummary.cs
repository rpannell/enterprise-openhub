namespace OpenHub.ProductManager.ReindexJob.Managers.Models;

public class ReindexSummary
{
    public int TenantCount { get; set; }
    public IReadOnlyList<Guid> FailedTenantIds { get; set; } = [];

    public bool AllTenantsSucceeded => FailedTenantIds.Count == 0;
}
