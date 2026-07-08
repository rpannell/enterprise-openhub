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

    public async Task<IReadOnlyList<TenantDto>> ListActiveAsync(CancellationToken ct = default)
    {
        var entities = await db.Tenants.AsNoTracking()
            .Where(t => t.IsActive && t.DeletedAt == null)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        return [.. entities.Select(mapper.ToDto)];
    }

    public async Task SyncTenantAsync(
        Guid tenantId, string realmSlug, string? name, bool isDeleted, CancellationToken ct = default)
    {
        var entity = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, ct);

        if (entity is null)
        {
            if (isDeleted) return; // nothing to delete

            entity = new TenantEntity
            {
                Id = tenantId,
                Slug = realmSlug,
                Name = string.IsNullOrWhiteSpace(name) ? realmSlug : name,
                PlanTier = "standard",
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            db.Tenants.Add(entity);
        }
        else if (isDeleted)
        {
            entity.IsActive = false;
            entity.DeletedAt = DateTimeOffset.UtcNow;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(name)) entity.Name = name;
            entity.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        // Invalidate rather than write-through: the next GetOrProvisionAsync repopulates from
        // the row we just wrote, which is simpler than duplicating the mapping here.
        await cache.RemoveAsync($"tenant:id:{tenantId}", ct);
    }

    public async Task SyncUserAsync(
        Guid tenantId, string uniqueId, string? email, string? firstName, string? lastName,
        bool isDeleted, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync(
                "SELECT set_config('app.current_tenant_id', {0}, true)", [tenantId.ToString()], ct);

            var entity = await db.Users.FirstOrDefaultAsync(
                u => u.TenantId == tenantId && u.UniqueId == uniqueId, ct);

            if (entity is null)
            {
                if (isDeleted)
                {
                    await tx.CommitAsync(ct);
                    return;
                }

                entity = new UserEntity
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    UniqueId = uniqueId,
                    Email = string.IsNullOrWhiteSpace(email) ? uniqueId : email,
                    FirstName = string.IsNullOrWhiteSpace(firstName) ? uniqueId : firstName,
                    LastName = string.IsNullOrWhiteSpace(lastName) ? "-" : lastName,
                    IsActive = true,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                db.Users.Add(entity);
            }
            else if (isDeleted)
            {
                entity.IsActive = false;
                entity.DeletedAt = DateTimeOffset.UtcNow;
                entity.UpdatedAt = DateTimeOffset.UtcNow;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(email)) entity.Email = email;
                if (!string.IsNullOrWhiteSpace(firstName)) entity.FirstName = firstName;
                if (!string.IsNullOrWhiteSpace(lastName)) entity.LastName = lastName;
                entity.UpdatedAt = DateTimeOffset.UtcNow;
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        await cache.RemoveAsync($"user:{tenantId}:{uniqueId}", ct);
    }

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
