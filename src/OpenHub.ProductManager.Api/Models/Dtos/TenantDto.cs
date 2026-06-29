namespace OpenHub.ProductManager.Api.Models.Dtos;

public class TenantDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string PlanTier { get; set; } = null!;
    public bool IsActive { get; set; }
}
