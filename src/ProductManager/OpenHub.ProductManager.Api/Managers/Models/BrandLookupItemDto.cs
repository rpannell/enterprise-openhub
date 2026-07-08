namespace OpenHub.ProductManager.Api.Managers.Models;

public class BrandLookupItemDto
{
    public Guid Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = null!;
    public Guid? ManufacturerId { get; set; }
}
