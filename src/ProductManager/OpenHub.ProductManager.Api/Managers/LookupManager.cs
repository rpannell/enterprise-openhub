using OpenHub.ProductManager.Api.Accessors.Interfaces;
using OpenHub.ProductManager.Api.Managers.Interfaces;
using OpenHub.ProductManager.Api.Managers.Models;
using OpenHub.ProductManager.Api.Mappers;

namespace OpenHub.ProductManager.Api.Managers;

public class LookupManager(
    ILookupCacheAccessor lookupCache,
    ICatalogLookupAccessor catalogLookup,
    LookupMapper mapper) : ILookupManager
{
    public async Task<LookupsDto> GetAllAsync(CancellationToken ct = default)
    {
        var conditionTypes = await lookupCache.GetConditionTypesAsync(ct);
        var availabilityStatuses = await lookupCache.GetAvailabilityStatusesAsync(ct);
        var unitsOfMeasure = await lookupCache.GetUnitsOfMeasureAsync(ct);
        var currencies = await lookupCache.GetCurrenciesAsync(ct);
        var priceTypes = await lookupCache.GetPriceTypesAsync(ct);
        var manufacturers = await catalogLookup.GetManufacturersAsync(ct);
        var brands = await catalogLookup.GetBrandsAsync(ct);

        return new LookupsDto
        {
            ConditionTypes = mapper.ToDtoList(conditionTypes),
            AvailabilityStatuses = mapper.ToDtoList(availabilityStatuses),
            UnitsOfMeasure = mapper.ToDtoList(unitsOfMeasure),
            Currencies = mapper.ToDtoList(currencies),
            PriceTypes = mapper.ToDtoList(priceTypes),
            Manufacturers = mapper.ToDtoList(manufacturers),
            Brands = mapper.ToDtoList(brands)
        };
    }
}
