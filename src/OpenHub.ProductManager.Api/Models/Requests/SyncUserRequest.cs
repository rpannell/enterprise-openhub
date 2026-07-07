using System.ComponentModel;

namespace OpenHub.ProductManager.Api.Models.Requests;

public class SyncUserRequest
{
    [Description("Tenant id the user belongs to — the Keycloak organization id.")]
    public Guid TenantId { get; set; }

    [Description("Keycloak user id.")]
    public string UniqueId { get; set; } = null!;

    [Description("User's email address. Ignored when IsDeleted is true.")]
    public string? Email { get; set; }

    [Description("User's first name. Ignored when IsDeleted is true.")]
    public string? FirstName { get; set; }

    [Description("User's last name. Ignored when IsDeleted is true.")]
    public string? LastName { get; set; }

    [Description("True to soft-delete the user instead of upserting it.")]
    public bool IsDeleted { get; set; }
}
