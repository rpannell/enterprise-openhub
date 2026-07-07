namespace OpenHub.ProductManager.ReindexJob.Accessors.Models;

/// <summary>
/// Shape returned by GET /api/v1/admin/tenants (ProductManager.Api's TenantDto). Field
/// names match the API's PascalCase System.Text.Json output exactly.
/// </summary>
public class TenantSummaryApiResponse
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string PlanTier { get; set; } = null!;
    public bool IsActive { get; set; }
}
