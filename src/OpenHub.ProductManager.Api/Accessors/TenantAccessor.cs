using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.ProductManager.Api.Accessors.Data;
using OpenHub.ProductManager.Api.Accessors.Interfaces;
using OpenHub.ProductManager.Api.Accessors.Models;
using OpenHub.ProductManager.Api.Mappers;
using OpenHub.ProductManager.Api.Managers.Models;

namespace OpenHub.ProductManager.Api.Accessors;

public class TenantAccessor(
    CatalogDbContext db,
    HybridCache cache,
    TenantMapper mapper,
    IKeyCloakRealmAccessor keyCloakRealmAccessor) : ITenantAccessor
{
    private static readonly HybridCacheEntryOptions CacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(24),
        LocalCacheExpiration = TimeSpan.FromMinutes(30)
    };

    public async Task<TenantDto?> GetOrProvisionAsync(
        Guid tenantId, string realmSlug, CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            $"tenant:id:{tenantId}",
            async _ => await ResolveOrProvisionAsync(tenantId, realmSlug, ct),
            CacheOptions,
            cancellationToken: ct);

    private async Task<TenantDto?> ResolveOrProvisionAsync(
        Guid tenantId, string realmSlug, CancellationToken ct)
    {
        // 1. Check the database first
        var entity = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive, ct);

        if (entity is not null)
            return mapper.ToDto(entity);

        // 2. Not in DB — fetch from Keycloak and auto-provision
        var realmInfo = await keyCloakRealmAccessor.GetRealmBySlugAsync(realmSlug, ct);
        if (realmInfo is null)
            return null;

        var newEntity = new TenantEntity
        {
            Id = tenantId,
            Slug = realmSlug,
            Name = string.IsNullOrWhiteSpace(realmInfo.DisplayName)
                ? realmSlug
                : realmInfo.DisplayName,
            PlanTier = "standard",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Tenants.Add(newEntity);
        await db.SaveChangesAsync(ct);

        return mapper.ToDto(newEntity);
    }
}
