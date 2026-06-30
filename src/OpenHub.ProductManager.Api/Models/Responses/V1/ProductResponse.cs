using System.ComponentModel;

namespace OpenHub.ProductManager.Api.Models.Responses.V1;

public class ProductResponse
{
    [Description("Unique product identifier.")]
    public Guid Id { get; set; }

    [Description("Stock-keeping unit, unique within the tenant.")]
    public string Sku { get; set; } = null!;

    [Description("Display name.")]
    public string Name { get; set; } = null!;

    [Description("Full product description.")]
    public string? Description { get; set; }

    [Description("Brief product summary for listing views.")]
    public string? ShortDescription { get; set; }

    [Description("Resolved manufacturer name from the caller's tenant-specific manufacturer list.")]
    public string? ManufacturerName { get; set; }

    [Description("Resolved brand name from the caller's tenant-specific brand list.")]
    public string? BrandName { get; set; }

    [Description("Resolved condition type (e.g. New, Used, Refurbished).")]
    public string? ConditionTypeName { get; set; }

    [Description("Resolved availability status (e.g. In Stock, Backordered).")]
    public string? AvailabilityStatusName { get; set; }

    [Description("Manufacturer's own part number.")]
    public string? ManufacturerPartNumber { get; set; }

    [Description("Whether the product is active. Inactive products are excluded from all query results.")]
    public bool IsActive { get; set; }

    [Description("Whether the product is published and visible to end customers.")]
    public bool IsPublished { get; set; }

    [Description("Product weight expressed in the unit specified by WeightUomCode.")]
    public decimal? Weight { get; set; }

    [Description("Unit of measure code for the Weight field (e.g. kg, lb).")]
    public string? WeightUomCode { get; set; }

    [Description("Timestamp when the product was created (UTC).")]
    public DateTimeOffset CreatedAt { get; set; }

    [Description("Timestamp when the product was last modified (UTC).")]
    public DateTimeOffset UpdatedAt { get; set; }

    [Description("Active prices for this product across all price types and currencies.")]
    public List<ProductPriceResponse> Prices { get; set; } = [];
}
