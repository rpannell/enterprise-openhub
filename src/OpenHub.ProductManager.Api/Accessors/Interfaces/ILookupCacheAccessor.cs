namespace OpenHub.ProductManager.Api.Accessors.Interfaces;

public interface ILookupCacheAccessor
{
    ValueTask<IReadOnlyDictionary<Guid, string>> GetManufacturerNamesAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyDictionary<Guid, string>> GetBrandNamesAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyDictionary<Guid, string>> GetConditionTypeNamesAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyDictionary<Guid, string>> GetAvailabilityStatusNamesAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyDictionary<Guid, string>> GetPriceTypeNamesAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyDictionary<Guid, string>> GetCurrencyCodesAsync(CancellationToken ct = default);
    ValueTask<IReadOnlyDictionary<Guid, string>> GetUomCodesAsync(CancellationToken ct = default);
    Task InvalidateAsync(string cacheKey, CancellationToken ct = default);
}
