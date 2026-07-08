namespace OpenHub.ProductManager.Api.Managers.Models;

public class UserSyncDto
{
    public Guid TenantId { get; set; }
    public string UniqueId { get; set; } = null!;
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsDeleted { get; set; }
}
