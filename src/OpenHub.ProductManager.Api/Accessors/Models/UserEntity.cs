namespace OpenHub.ProductManager.Api.Accessors.Models;

public class UserEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string UniqueId { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
