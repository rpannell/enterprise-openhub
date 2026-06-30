using System.ComponentModel;

namespace OpenHub.ProductManager.Api.Models.Responses.V1;

public class LookupsResponse
{
    [Description("Platform-wide condition types (e.g. New, Used, Refurbished).")]
    public List<LookupItemResponse> ConditionTypes { get; set; } = [];

    [Description("Platform-wide availability statuses (e.g. In Stock, Backordered).")]
    public List<LookupItemResponse> AvailabilityStatuses { get; set; } = [];

    [Description("Platform-wide units of measure across all unit types (weight, length, volume, quantity, area).")]
    public List<UomLookupItemResponse> UnitsOfMeasure { get; set; } = [];

    [Description("Platform-wide currencies (ISO 4217).")]
    public List<CurrencyLookupItemResponse> Currencies { get; set; } = [];

    [Description("Platform-wide price types (e.g. Retail, Wholesale, Cost).")]
    public List<LookupItemResponse> PriceTypes { get; set; } = [];

    [Description("Manufacturers managed by the caller's tenant.")]
    public List<LookupItemResponse> Manufacturers { get; set; } = [];

    [Description("Brands managed by the caller's tenant.")]
    public List<BrandLookupItemResponse> Brands { get; set; } = [];
}
