using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using OpenHub.ProductManager.Api.Accessors.Data;
using OpenHub.ProductManager.Api.Accessors.Interfaces;
using OpenHub.ProductManager.Api.Accessors.Models;

namespace OpenHub.ProductManager.Api.Accessors;

public class CatalogLookupAccessor(CatalogDbContext db, IHttpContextAccessor http, HybridCache cache) : ICatalogLookupAccessor
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(1),
        LocalCacheExpiration = TimeSpan.FromMinutes(5)
    };

    private Guid TenantId =>
        http.HttpContext?.Items["TenantId"] as Guid?
            ?? throw new InvalidOperationException("Tenant context not set. Ensure TenantMiddleware is registered.");

    private async Task SetTenantContextAsync(Guid tenantId, CancellationToken ct)
        => await db.Database.ExecuteSqlRawAsync(
            "SET LOCAL app.current_tenant_id = {0}", [tenantId.ToString()], ct);

    public ValueTask<IReadOnlyList<ManufacturerEntity>> GetManufacturersAsync(CancellationToken ct = default)
    {
        var tenantId = TenantId;
        return cache.GetOrCreateAsync(
            $"catalog:manufacturers:tenant:{tenantId}",
            async _ =>
            {
                await SetTenantContextAsync(tenantId, ct);
                return (IReadOnlyList<ManufacturerEntity>)await db.Manufacturers.AsNoTracking()
                    .Where(m => m.TenantId == tenantId && m.IsActive)
                    .OrderBy(m => m.Name)
                    .ToListAsync(ct);
            },
            CacheOptions, cancellationToken: ct);
    }

    public ValueTask<IReadOnlyList<BrandEntity>> GetBrandsAsync(CancellationToken ct = default)
    {
        var tenantId = TenantId;
        return cache.GetOrCreateAsync(
            $"catalog:brands:tenant:{tenantId}",
            async _ =>
            {
                await SetTenantContextAsync(tenantId, ct);
                return (IReadOnlyList<BrandEntity>)await db.Brands.AsNoTracking()
                    .Where(b => b.TenantId == tenantId && b.IsActive)
                    .OrderBy(b => b.Name)
                    .ToListAsync(ct);
            },
            CacheOptions, cancellationToken: ct);
    }
}
