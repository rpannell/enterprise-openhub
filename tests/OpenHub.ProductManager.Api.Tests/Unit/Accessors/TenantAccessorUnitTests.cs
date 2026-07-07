using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.ProductManager.Api.Accessors;
using OpenHub.ProductManager.Api.Accessors.Data;
using OpenHub.ProductManager.Api.Accessors.Models;
using OpenHub.ProductManager.Api.Mappers;

namespace OpenHub.ProductManager.Api.Tests.Unit.Accessors;

/// <summary>
/// Covers only TenantAccessor's tenant-related methods (ListActiveAsync, SyncTenantAsync) —
/// catalog.tenants is intentionally excluded from Row Level Security, so these are the only
/// TenantAccessor methods with no raw `SET LOCAL`/`set_config` SQL call, which EF Core InMemory
/// can't execute. Every other TenantAccessor method (user-related, RLS-scoped) is covered only
/// by the Testcontainers Postgres integration tier — see Integration/Accessors/TenantAccessorIntegrationTests.
/// </summary>
public class TenantAccessorUnitTests
{
    private static CatalogDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CatalogDbContext(options);
    }

    private static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private static TenantAccessor CreateAccessor(CatalogDbContext db)
        => new(db, CreateHybridCache(), new TenantMapper(), Substitute.For<IKeyCloakRealmAccessor>());

    private static TenantEntity MakeTenant(bool isActive = true, DateTimeOffset? deletedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        Slug = "client-a",
        Name = "Client A",
        PlanTier = "standard",
        IsActive = isActive,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        DeletedAt = deletedAt,
    };

    [Fact]
    public async Task ListActiveAsync_ExcludesInactiveTenants()
    {
        await using var db = CreateDbContext();
        var active = MakeTenant(isActive: true);
        var inactive = MakeTenant(isActive: false);
        db.Tenants.AddRange(active, inactive);
        await db.SaveChangesAsync();
        var accessor = CreateAccessor(db);

        var result = await accessor.ListActiveAsync();

        result.ShouldHaveSingleItem();
        result[0].Id.ShouldBe(active.Id);
    }

    [Fact]
    public async Task ListActiveAsync_ExcludesSoftDeletedTenants()
    {
        await using var db = CreateDbContext();
        var active = MakeTenant();
        var deleted = MakeTenant(deletedAt: DateTimeOffset.UtcNow);
        db.Tenants.AddRange(active, deleted);
        await db.SaveChangesAsync();
        var accessor = CreateAccessor(db);

        var result = await accessor.ListActiveAsync();

        result.ShouldHaveSingleItem();
        result[0].Id.ShouldBe(active.Id);
    }

    [Fact]
    public async Task SyncTenantAsync_TenantMissing_NotDeleted_InsertsNewTenant()
    {
        await using var db = CreateDbContext();
        var accessor = CreateAccessor(db);
        var tenantId = Guid.NewGuid();

        await accessor.SyncTenantAsync(tenantId, "client-a", "Client A", isDeleted: false);

        var entity = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        entity.ShouldNotBeNull();
        entity.Name.ShouldBe("Client A");
        entity.Slug.ShouldBe("client-a");
        entity.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task SyncTenantAsync_TenantMissing_NameNull_FallsBackToRealmSlug()
    {
        await using var db = CreateDbContext();
        var accessor = CreateAccessor(db);
        var tenantId = Guid.NewGuid();

        await accessor.SyncTenantAsync(tenantId, "client-a", name: null, isDeleted: false);

        var entity = await db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId);
        entity.ShouldNotBeNull();
        entity.Name.ShouldBe("client-a");
    }

    [Fact]
    public async Task SyncTenantAsync_TenantMissing_IsDeletedTrue_IsNoOp()
    {
        await using var db = CreateDbContext();
        var accessor = CreateAccessor(db);
        var tenantId = Guid.NewGuid();

        await accessor.SyncTenantAsync(tenantId, "client-a", "Client A", isDeleted: true);

        (await db.Tenants.AnyAsync(t => t.Id == tenantId)).ShouldBeFalse();
    }

    [Fact]
    public async Task SyncTenantAsync_TenantExists_NotDeleted_UpdatesName()
    {
        await using var db = CreateDbContext();
        var existing = MakeTenant();
        db.Tenants.Add(existing);
        await db.SaveChangesAsync();
        var accessor = CreateAccessor(db);

        await accessor.SyncTenantAsync(existing.Id, existing.Slug, "Renamed Client", isDeleted: false);

        var entity = await db.Tenants.FirstAsync(t => t.Id == existing.Id);
        entity.Name.ShouldBe("Renamed Client");
        entity.IsActive.ShouldBeTrue();
        entity.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task SyncTenantAsync_TenantExists_NameNull_LeavesExistingNameUnchanged()
    {
        await using var db = CreateDbContext();
        var existing = MakeTenant();
        db.Tenants.Add(existing);
        await db.SaveChangesAsync();
        var accessor = CreateAccessor(db);

        await accessor.SyncTenantAsync(existing.Id, existing.Slug, name: null, isDeleted: false);

        var entity = await db.Tenants.FirstAsync(t => t.Id == existing.Id);
        entity.Name.ShouldBe(existing.Name);
    }

    [Fact]
    public async Task SyncTenantAsync_TenantExists_IsDeletedTrue_SoftDeletesAndDeactivates()
    {
        await using var db = CreateDbContext();
        var existing = MakeTenant();
        db.Tenants.Add(existing);
        await db.SaveChangesAsync();
        var accessor = CreateAccessor(db);

        await accessor.SyncTenantAsync(existing.Id, existing.Slug, existing.Name, isDeleted: true);

        var entity = await db.Tenants.FirstAsync(t => t.Id == existing.Id);
        entity.IsActive.ShouldBeFalse();
        entity.DeletedAt.ShouldNotBeNull();
    }
}
