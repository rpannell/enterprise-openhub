using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using OpenHub.ProductManager.Api.Accessors.Data;
using OpenHub.ProductManager.Api.Accessors.Interfaces;
using OpenHub.ProductManager.Api.Mappers;
using OpenHub.ProductManager.Api.Models.Entities;
using OpenHub.ProductManager.Api.Models.Dtos;

namespace OpenHub.ProductManager.Api.Accessors;

public class TenantAccessor(CatalogDbContext db, HybridCache cache, TenantMapper mapper) : ITenantAccessor
{
    public async Task<TenantDto?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            $"tenant:slug:{slug}",
            async _ =>
            {
                var entity = await db.Tenants.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.Slug == slug && t.IsActive, ct);
                return entity is null ? null : mapper.ToDto(entity);
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromHours(1),
                LocalCacheExpiration = TimeSpan.FromMinutes(10)
            },
            cancellationToken: ct);
}
