using OpenHub.ProductManager.Api.Accessors.Models;

namespace OpenHub.ProductManager.Api.Accessors.Interfaces;

public interface ILookupCacheAccessor
{
    // Id → name dictionaries, used to hydrate denormalized names onto product responses
    ValueTask<IReadOnlyDictionary<Guid, string>> GetConditionTypeNamesAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyDictionary<Guid, string>> GetAvailabilityStatusNamesAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyDictionary<Guid, string>> GetPriceTypeNamesAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyDictionary<Guid, string>> GetCurrencyCodesAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyDictionary<Guid, string>> GetUomCodesAsync(CancellationToken ct = default);

    // Full rows, used by the lookup endpoint to populate UI dropdowns
    ValueTask<IReadOnlyList<LookupItemRow>> GetConditionTypesAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyList<LookupItemRow>> GetAvailabilityStatusesAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyList<LookupItemRow>> GetPriceTypesAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyList<CurrencyLookupRow>> GetCurrenciesAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyList<UomLookupRow>> GetUnitsOfMeasureAsync(CancellationToken ct = default);

    Task InvalidateAsync(string cacheKey, CancellationToken ct = default);
}
