namespace OpenHub.ProductManager.Api.Accessors.Models;

public class BrandEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? ManufacturerId { get; set; }
    public string Name { get; set; } = null!;
    public string? Code { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
