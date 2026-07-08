using System.ComponentModel;

namespace OpenHub.ProductManager.Api.Models.Requests;

public class SyncTenantRequest
{
    [Description("Tenant id — the Keycloak organization id.")]
    public Guid TenantId { get; set; }

    [Description("Keycloak realm the organization belongs to, e.g. \"client-a\".")]
    public string RealmSlug { get; set; } = null!;

    [Description("Organization display name. Ignored when IsDeleted is true.")]
    public string? Name { get; set; }

    [Description("True to soft-delete the tenant instead of upserting it.")]
    public bool IsDeleted { get; set; }
}
