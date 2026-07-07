namespace OpenHub.ProductManager.IdentityEventProcessor.Accessors.Models;

/// <summary>Wire shape for ProductManager.Api's POST /api/v1/admin/users/sync — mirrors SyncUserRequest there.</summary>
public sealed class SyncUserApiRequest
{
    public Guid TenantId { get; set; }
    public string UniqueId { get; set; } = null!;
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsDeleted { get; set; }
}
