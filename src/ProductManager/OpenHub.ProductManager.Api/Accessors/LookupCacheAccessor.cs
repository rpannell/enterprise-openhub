using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using OpenHub.ProductManager.Api.Accessors.Data;
using OpenHub.ProductManager.Api.Accessors.Interfaces;
using OpenHub.ProductManager.Api.Accessors.Models;

namespace OpenHub.ProductManager.Api.Accessors;

public class LookupCacheAccessor(CatalogDbContext db, HybridCache cache) : ILookupCacheAccessor
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    public ValueTask<IReadOnlyDictionary<Guid, string>> GetConditionTypeNamesAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync("ref:condition_types",
            async _ => (IReadOnlyDictionary<Guid, string>)await db.Database
                .SqlQueryRaw<NameRow>("SELECT id, name FROM ref.condition_types")
                .ToDictionaryAsync(r => r.Id, r => r.Name, ct),
            CacheOptions, cancellationToken: ct);

    public ValueTask<IReadOnlyDictionary<Guid, string>> GetAvailabilityStatusNamesAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync("ref:availability_statuses",
            async _ => (IReadOnlyDictionary<Guid, string>)await db.Database
                .SqlQueryRaw<NameRow>("SELECT id, name FROM ref.availability_statuses")
                .ToDictionaryAsync(r => r.Id, r => r.Name, ct),
            CacheOptions, cancellationToken: ct);

    public ValueTask<IReadOnlyDictionary<Guid, string>> GetPriceTypeNamesAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync("ref:price_types",
            async _ => (IReadOnlyDictionary<Guid, string>)await db.Database
                .SqlQueryRaw<NameRow>("SELECT id, name FROM ref.price_types")
                .ToDictionaryAsync(r => r.Id, r => r.Name, ct),
            CacheOptions, cancellationToken: ct);

    public ValueTask<IReadOnlyDictionary<Guid, string>> GetCurrencyCodesAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync("ref:currencies",
            async _ => (IReadOnlyDictionary<Guid, string>)await db.Database
                .SqlQueryRaw<NameRow>("SELECT id, code AS name FROM ref.currencies WHERE is_active = TRUE")
                .ToDictionaryAsync(r => r.Id, r => r.Name, ct),
            CacheOptions, cancellationToken: ct);

    public ValueTask<IReadOnlyDictionary<Guid, string>> GetUomCodesAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync("ref:uom",
            async _ => (IReadOnlyDictionary<Guid, string>)await db.Database
                .SqlQueryRaw<NameRow>("SELECT id, code AS name FROM ref.units_of_measure")
                .ToDictionaryAsync(r => r.Id, r => r.Name, ct),
            CacheOptions, cancellationToken: ct);

    public ValueTask<IReadOnlyList<LookupItemRow>> GetConditionTypesAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync("ref:condition_types:list",
            async _ => (IReadOnlyList<LookupItemRow>)await db.Database
                .SqlQueryRaw<LookupItemRow>("SELECT id, code, name FROM ref.condition_types ORDER BY sort_order")
                .ToListAsync(ct),
            CacheOptions, cancellationToken: ct);

    public ValueTask<IReadOnlyList<LookupItemRow>> GetAvailabilityStatusesAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync("ref:availability_statuses:list",
            async _ => (IReadOnlyList<LookupItemRow>)await db.Database
                .SqlQueryRaw<LookupItemRow>("SELECT id, code, name FROM ref.availability_statuses ORDER BY sort_order")
                .ToListAsync(ct),
            CacheOptions, cancellationToken: ct);

    public ValueTask<IReadOnlyList<LookupItemRow>> GetPriceTypesAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync("ref:price_types:list",
            async _ => (IReadOnlyList<LookupItemRow>)await db.Database
                .SqlQueryRaw<LookupItemRow>("SELECT id, code, name FROM ref.price_types ORDER BY sort_order")
                .ToListAsync(ct),
            CacheOptions, cancellationToken: ct);

    public ValueTask<IReadOnlyList<CurrencyLookupRow>> GetCurrenciesAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync("ref:currencies:list",
            async _ => (IReadOnlyList<CurrencyLookupRow>)await db.Database
                .SqlQueryRaw<CurrencyLookupRow>("SELECT id, code, name, symbol FROM ref.currencies WHERE is_active = TRUE ORDER BY code")
                .ToListAsync(ct),
            CacheOptions, cancellationToken: ct);

    public ValueTask<IReadOnlyList<UomLookupRow>> GetUnitsOfMeasureAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync("ref:uom:list",
            async _ => (IReadOnlyList<UomLookupRow>)await db.Database
                .SqlQueryRaw<UomLookupRow>("SELECT id, code, name, abbreviation, uom_type AS \"UomType\" FROM ref.units_of_measure ORDER BY uom_type, name")
                .ToListAsync(ct),
            CacheOptions, cancellationToken: ct);

    public async Task InvalidateAsync(string cacheKey, CancellationToken ct = default)
        => await cache.RemoveAsync(cacheKey, ct);

    private record NameRow(Guid Id, string Name);
}
