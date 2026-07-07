namespace OpenHub.ProductManager.Api.Managers.Models;

public class TenantSyncDto
{
    public Guid TenantId { get; set; }
    public string RealmSlug { get; set; } = null!;
    public string? Name { get; set; }
    public bool IsDeleted { get; set; }
}
