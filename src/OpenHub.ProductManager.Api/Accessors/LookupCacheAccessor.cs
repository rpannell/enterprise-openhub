using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using OpenHub.ProductManager.Api.Accessors.Data;
using OpenHub.ProductManager.Api.Accessors.Interfaces;

namespace OpenHub.ProductManager.Api.Accessors;

public class LookupCacheAccessor(CatalogDbContext db, HybridCache cache) : ILookupCacheAccessor
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    public ValueTask<IReadOnlyDictionary<Guid, string>> GetManufacturerNamesAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync("ref:manufacturers",
            async _ => (IReadOnlyDictionary<Guid, string>)await db.Database
                .SqlQueryRaw<LookupRow>("SELECT id, name FROM ref.manufacturers WHERE deleted_at IS NULL")
                .ToDictionaryAsync(r => r.Id, r => r.Name, ct),
            CacheOptions, cancellationToken: ct);

    public ValueTask<IReadOnlyDictionary<Guid, string>> GetBrandNamesAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync("ref:brands",
            async _ => (IReadOnlyDictionary<Guid, string>)await db.Database
                .SqlQueryRaw<LookupRow>("SELECT id, name FROM ref.brands WHERE deleted_at IS NULL")
                .ToDictionaryAsync(r => r.Id, r => r.Name, ct),
            CacheOptions, cancellationToken: ct);

    public ValueTask<IReadOnlyDictionary<Guid, string>> GetConditionTypeNamesAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync("ref:condition_types",
            async _ => (IReadOnlyDictionary<Guid, string>)await db.Database
                .SqlQueryRaw<LookupRow>("SELECT id, name FROM ref.condition_types")
                .ToDictionaryAsync(r => r.Id, r => r.Name, ct),
            CacheOptions, cancellationToken: ct);

    public ValueTask<IReadOnlyDictionary<Guid, string>> GetAvailabilityStatusNamesAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync("ref:availability_statuses",
            async _ => (IReadOnlyDictionary<Guid, string>)await db.Database
                .SqlQueryRaw<LookupRow>("SELECT id, name FROM ref.availability_statuses")
                .ToDictionaryAsync(r => r.Id, r => r.Name, ct),
            CacheOptions, cancellationToken: ct);

    public ValueTask<IReadOnlyDictionary<Guid, string>> GetPriceTypeNamesAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync("ref:price_types",
            async _ => (IReadOnlyDictionary<Guid, string>)await db.Database
                .SqlQueryRaw<LookupRow>("SELECT id, name FROM ref.price_types")
                .ToDictionaryAsync(r => r.Id, r => r.Name, ct),
            CacheOptions, cancellationToken: ct);

    public ValueTask<IReadOnlyDictionary<Guid, string>> GetCurrencyCodesAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync("ref:currencies",
            async _ => (IReadOnlyDictionary<Guid, string>)await db.Database
                .SqlQueryRaw<LookupRow>("SELECT id, code AS name FROM ref.currencies WHERE is_active = TRUE")
                .ToDictionaryAsync(r => r.Id, r => r.Name, ct),
            CacheOptions, cancellationToken: ct);

    public ValueTask<IReadOnlyDictionary<Guid, string>> GetUomCodesAsync(CancellationToken ct = default)
        => cache.GetOrCreateAsync("ref:uom",
            async _ => (IReadOnlyDictionary<Guid, string>)await db.Database
                .SqlQueryRaw<LookupRow>("SELECT id, code AS name FROM ref.units_of_measure")
                .ToDictionaryAsync(r => r.Id, r => r.Name, ct),
            CacheOptions, cancellationToken: ct);

    public async Task InvalidateAsync(string cacheKey, CancellationToken ct = default)
        => await cache.RemoveAsync(cacheKey, ct);

    private record LookupRow(Guid Id, string Name);
}
