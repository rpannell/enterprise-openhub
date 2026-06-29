namespace OpenHub.ProductManager.Api.Models.Responses;

public class ProductResponse
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? ShortDescription { get; set; }
    public string? ManufacturerName { get; set; }
    public string? BrandName { get; set; }
    public string? ConditionTypeName { get; set; }
    public string? AvailabilityStatusName { get; set; }
    public string? ManufacturerPartNumber { get; set; }
    public bool IsActive { get; set; }
    public bool IsPublished { get; set; }
    public decimal? Weight { get; set; }
    public string? WeightUomCode { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<ProductPriceResponse> Prices { get; set; } = [];
}
