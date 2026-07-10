using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Npgsql;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.CRM.Api.Accessors.Data;
using OpenHub.CRM.Api.Accessors.Interfaces;
using OpenHub.CRM.Api.Accessors.Models;
using OpenHub.CRM.Api.Mappers;
using OpenHub.CRM.Api.Managers.Models;

namespace OpenHub.CRM.Api.Accessors;

public class TenantAccessor(
    CrmDbContext db,
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

    // No manual key prefix needed here — Program.cs sets RedisCacheOptions.InstanceName = "CRM:",
    // which the underlying IDistributedCache applies to every key this service sends to Redis.
    // That's what keeps "tenant:id:{tenantId}" from colliding with ProductManager.Api's
    // identically-shaped cache entries on the same shared Redis instance (see Program.cs).

    public async Task<TenantDto?> GetOrProvisionAsync(
        Guid tenantId, string realmSlug, CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            $"tenant:id:{tenantId}",
            async _ => await ResolveOrProvisionTenantAsync(tenantId, realmSlug, ct),
            TenantCacheOptions,
            cancellationToken: ct);

    public async Task<Guid?> GetOrProvisionUserAsync(
        Guid tenantId, string realmSlug, string uniqueId, string email, string firstName, string lastName,
        CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            $"user:{tenantId}:{uniqueId}",
            async _ => await ResolveOrProvisionUserAsync(tenantId, realmSlug, uniqueId, email, firstName, lastName, ct),
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
        Guid tenantId, string realmSlug, string uniqueId, string email, string firstName, string lastName,
        CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            for (var attempt = 0; attempt < 2; attempt++)
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

                try
                {
                    await db.SaveChangesAsync(ct);
                    await tx.CommitAsync(ct);
                    return (Guid?)newUser.Id;
                }
                catch (DbUpdateException ex) when (attempt == 0 && IsTenantForeignKeyViolation(ex))
                {
                    // The cached tenant said it exists, but Postgres disagrees — typically local
                    // dev, where the Redis volume survives a Postgres reset/reseed. Evict the
                    // stale entry, re-provision the tenant, and retry the insert once before
                    // surfacing a 500 for something that's self-healable.
                    db.Entry(newUser).State = EntityState.Detached;
                    await cache.RemoveAsync($"tenant:id:{tenantId}", ct);
                    await ResolveOrProvisionTenantAsync(tenantId, realmSlug, ct);
                }
                catch (DbUpdateException ex) when (attempt == 0 && IsDuplicateUser(ex))
                {
                    // Lost a race with a concurrent request provisioning the same user — e.g. two
                    // pods handling parallel first-time requests, where HybridCache's stampede
                    // protection only coalesces calls within one process. The other request's
                    // insert already committed, so the next loop iteration's "existing" check
                    // will pick it up.
                    db.Entry(newUser).State = EntityState.Detached;
                }
            }

            return null;
        });
    }

    private static bool IsTenantForeignKeyViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: "23503", ConstraintName: "users_tenant_id_fkey" };

    private static bool IsDuplicateUser(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "uq_crm_users_tenant_unique_id" };
}
