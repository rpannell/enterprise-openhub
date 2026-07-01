using System.ComponentModel;

namespace OpenHub.ProductManager.Api.Models.Requests;

public class CreateProductRequest
{
    [Description("Stock-keeping unit identifier. Must be unique within the tenant. Max 100 characters.")]
    public string Sku { get; set; } = null!;

    [Description("Display name shown to end users. Max 500 characters.")]
    public string Name { get; set; } = null!;

    [Description("Full product description. Max 4000 characters.")]
    public string? Description { get; set; }

    [Description("Brief product summary for listing views. Max 500 characters.")]
    public string? ShortDescription { get; set; }

    [Description("Manufacturer name as it appears in the tenant's manufacturer catalog. Max 200 characters.")]
    public string Manufacturer { get; set; } = null!;

    [Description("Brand name as it appears in the tenant's brand catalog. Max 200 characters.")]
    public string Brand { get; set; } = null!;

    [Description("Reference to a condition type (e.g. New, Used, Refurbished) in the ref.condition_types lookup table.")]
    public Guid? ConditionTypeId { get; set; }

    [Description("Reference to an availability status (e.g. In Stock, Backordered) in the ref.availability_statuses lookup table.")]
    public Guid? AvailabilityStatusId { get; set; }

    [Description("Reference to the base unit of measure in the ref.units_of_measure lookup table.")]
    public Guid? BaseUomId { get; set; }

    [Description("The manufacturer's own part number for this product.")]
    public string? ManufacturerPartNumber { get; set; }

    [Description("Product weight expressed in the unit specified by WeightUomId. Must be greater than 0 when provided.")]
    public decimal? Weight { get; set; }

    [Description("Unit of measure for the Weight field. Reference to ref.units_of_measure.")]
    public Guid? WeightUomId { get; set; }
}
