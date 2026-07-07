namespace OpenHub.ProductManager.IdentityEventProcessor.Accessors.Models;

/// <summary>Wire shape for ProductManager.Api's POST /api/v1/admin/tenants/sync — mirrors SyncTenantRequest there.</summary>
public sealed class SyncTenantApiRequest
{
    public Guid TenantId { get; set; }
    public string RealmSlug { get; set; } = null!;
    public string? Name { get; set; }
    public bool IsDeleted { get; set; }
}
