namespace OpenHub.ProductManager.Api.Models.Requests;

public class CreateProductRequest
{
    public string Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string? ShortDescription { get; set; }
    public Guid? ManufacturerId { get; set; }
    public Guid? BrandId { get; set; }
    public Guid? ConditionTypeId { get; set; }
    public Guid? AvailabilityStatusId { get; set; }
    public Guid? BaseUomId { get; set; }
    public string? ManufacturerPartNumber { get; set; }
    public decimal? Weight { get; set; }
    public Guid? WeightUomId { get; set; }
}
