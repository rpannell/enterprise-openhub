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
            "SELECT set_config('app.current_tenant_id', {0}, true)", [tenantId.ToString()], ct);

    public ValueTask<IReadOnlyList<ManufacturerEntity>> GetManufacturersAsync(CancellationToken ct = default)
    {
        var tenantId = TenantId;
        return cache.GetOrCreateAsync(
            $"catalog:manufacturers:tenant:{tenantId}",
            async _ =>
            {
                var strategy = db.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await db.Database.BeginTransactionAsync(ct);
                    await SetTenantContextAsync(tenantId, ct);
                    var results = (IReadOnlyList<ManufacturerEntity>)await db.Manufacturers.AsNoTracking()
                        .Where(m => m.TenantId == tenantId && m.IsActive)
                        .OrderBy(m => m.Name)
                        .ToListAsync(ct);
                    await tx.CommitAsync(ct);
                    return results;
                });
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
                var strategy = db.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await db.Database.BeginTransactionAsync(ct);
                    await SetTenantContextAsync(tenantId, ct);
                    var results = (IReadOnlyList<BrandEntity>)await db.Brands.AsNoTracking()
                        .Where(b => b.TenantId == tenantId && b.IsActive)
                        .OrderBy(b => b.Name)
                        .ToListAsync(ct);
                    await tx.CommitAsync(ct);
                    return results;
                });
            },
            CacheOptions, cancellationToken: ct);
    }

    public async Task<ManufacturerEntity> InsertManufacturerAsync(string name, Guid tenantId, CancellationToken ct = default)
    {
        ManufacturerEntity? entity = null;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            entity = new ManufacturerEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = name,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            db.Manufacturers.Add(entity);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        await cache.RemoveAsync($"catalog:manufacturers:tenant:{tenantId}", ct);
        return entity!;
    }

    public async Task<BrandEntity> InsertBrandAsync(string name, Guid tenantId, CancellationToken ct = default)
    {
        BrandEntity? entity = null;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            entity = new BrandEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = name,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            db.Brands.Add(entity);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        await cache.RemoveAsync($"catalog:brands:tenant:{tenantId}", ct);
        return entity!;
    }
}
