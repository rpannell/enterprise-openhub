namespace OpenHub.ProductManager.Api.Models.Requests;

public class UpdateProductRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? ShortDescription { get; set; }
    public Guid? ManufacturerId { get; set; }
    public Guid? BrandId { get; set; }
    public Guid? ConditionTypeId { get; set; }
    public Guid? AvailabilityStatusId { get; set; }
    public Guid? BaseUomId { get; set; }
    public string? ManufacturerPartNumber { get; set; }
    public bool? IsActive { get; set; }
    public bool? IsPublished { get; set; }
    public decimal? Weight { get; set; }
    public Guid? WeightUomId { get; set; }
}
