using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.SalesOrg.Api.Accessors.Data;
using OpenHub.SalesOrg.Api.Accessors.Interfaces;
using OpenHub.SalesOrg.Api.Accessors.Models;
using OpenHub.SalesOrg.Api.Mappers;
using OpenHub.SalesOrg.Api.Managers.Models;

namespace OpenHub.SalesOrg.Api.Accessors;

public class TenantAccessor(
    SalesOrgDbContext db,
    HybridCache cache,
    TenantMapper mapper,
    IKeyCloakRealmAccessor keyCloakRealmAccessor) : ITenantAccessor
{
    private static readonly HybridCacheEntryOptions TenantCacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(24),
        LocalCacheExpiration = TimeSpan.FromMinutes(30)
    };

    private static readonly HybridCacheEntryOptions UserCacheOptions = new()
    {
        Expiration = TimeSpan.FromHours(24),
        LocalCacheExpiration = TimeSpan.FromHours(1)
    };

    // No manual key prefix needed here — Program.cs sets RedisCacheOptions.InstanceName =
    // ApiConst.RedisInstanceName ("SalesOrg:"), which the underlying IDistributedCache applies to
    // every key this service sends to Redis. That's what keeps "tenant:id:{tenantId}" from
    // colliding with CRM.Api's/ProductManager.Api's identically-shaped cache entries on the same
    // shared Redis instance (see Program.cs).

    public async Task<TenantDto?> GetOrProvisionAsync(
        Guid tenantId, string realmSlug, CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            $"tenant:id:{tenantId}",
            async _ => await ResolveOrProvisionTenantAsync(tenantId, realmSlug, ct),
            TenantCacheOptions,
            cancellationToken: ct);

    public async Task<Guid?> GetOrProvisionUserAsync(
        Guid tenantId, string uniqueId, string email, string firstName, string lastName,
        CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            $"user:{tenantId}:{uniqueId}",
            async _ => await ResolveOrProvisionUserAsync(tenantId, uniqueId, email, firstName, lastName, ct),
            UserCacheOptions,
            cancellationToken: ct);

    private async Task<TenantDto?> ResolveOrProvisionTenantAsync(
        Guid tenantId, string realmSlug, CancellationToken ct)
    {
        var entity = await db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId && t.IsActive, ct);

        if (entity is not null)
            return mapper.ToDto(entity);

        var realmInfo = await keyCloakRealmAccessor.GetRealmBySlugAsync(realmSlug, ct);
        if (realmInfo is null)
            return null;

        var newEntity = new TenantEntity
        {
            Id = tenantId,
            Slug = realmSlug,
            Name = string.IsNullOrWhiteSpace(realmInfo.DisplayName) ? realmSlug : realmInfo.DisplayName,
            PlanTier = "standard",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        db.Tenants.Add(newEntity);
        await db.SaveChangesAsync(ct);

        return mapper.ToDto(newEntity);
    }

    private async Task<Guid?> ResolveOrProvisionUserAsync(
        Guid tenantId, string uniqueId, string email, string firstName, string lastName,
        CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_tenant_id', {0}, true)", [tenantId.ToString()], ct);

            var existing = await db.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.UniqueId == uniqueId, ct);

            if (existing is not null)
            {
                await tx.CommitAsync(ct);
                return (Guid?)existing.Id;
            }

            var newUser = new UserEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                UniqueId = uniqueId,
                Email = email,
                FirstName = string.IsNullOrWhiteSpace(firstName) ? uniqueId : firstName,
                LastName = string.IsNullOrWhiteSpace(lastName) ? "-" : lastName,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            db.Users.Add(newUser);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return (Guid?)newUser.Id;
        });
    }
}
