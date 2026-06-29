namespace OpenHub.ProductManager.Api.Models.Entities;

public class TenantEntity
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string PlanTier { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
