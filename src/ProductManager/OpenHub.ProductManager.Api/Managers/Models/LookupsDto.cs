namespace OpenHub.ProductManager.Api.Managers.Models;

public class LookupsDto
{
    public List<LookupItemDto> ConditionTypes { get; set; } = [];
    public List<LookupItemDto> AvailabilityStatuses { get; set; } = [];
    public List<UomLookupItemDto> UnitsOfMeasure { get; set; } = [];
    public List<CurrencyLookupItemDto> Currencies { get; set; } = [];
    public List<LookupItemDto> PriceTypes { get; set; } = [];
    public List<LookupItemDto> Manufacturers { get; set; } = [];
    public List<BrandLookupItemDto> Brands { get; set; } = [];
}
