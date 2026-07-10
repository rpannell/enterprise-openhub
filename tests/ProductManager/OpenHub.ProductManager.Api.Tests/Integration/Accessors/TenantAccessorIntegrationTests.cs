using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.ProductManager.Api.Accessors;
using OpenHub.ProductManager.Api.Accessors.Data;
using OpenHub.ProductManager.Api.Mappers;
using OpenHub.ProductManager.Api.Tests.Integration.Fixtures;

namespace OpenHub.ProductManager.Api.Tests.Integration.Accessors;

/// <summary>
/// Covers TenantAccessor's user-related methods (GetOrProvisionUserAsync, SyncUserAsync) —
/// unlike catalog.tenants, catalog.users IS RLS-scoped (opens with a raw
/// `SET LOCAL app.current_tenant_id` call), so these can only run against real Postgres.
/// See Unit/Accessors/TenantAccessorUnitTests for the non-RLS tenant-related methods.
///
/// Assertions read back results via <see cref="PostgresCollectionFixture.CreateSuperuserDbContext"/>,
/// not the app_user context the accessor itself uses — catalog.users is RLS-protected, and once
/// the accessor's own transaction (which sets app.current_tenant_id) commits, a bare query on
/// that connection outside of any transaction would fail the RLS policy's `::UUID` cast (a
/// touched-then-reset custom GUC reads back as an empty string, not "unset").
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollectionDefinition.Name)]
public class TenantAccessorIntegrationTests(PostgresCollectionFixture fixture)
{
    private static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private static TenantAccessor CreateAccessor(CatalogDbContext db)
        => new(db, CreateHybridCache(), new TenantMapper(), Substitute.For<IKeyCloakRealmAccessor>());

    private async Task<Guid> SeedTenantAsync(string slug)
    {
        var tenantId = Guid.NewGuid();
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO catalog.tenants (id, slug, name) VALUES (@id, @slug, @name)", connection);
        cmd.Parameters.AddWithValue("id", tenantId);
        cmd.Parameters.AddWithValue("slug", slug);
        cmd.Parameters.AddWithValue("name", slug);
        await cmd.ExecuteNonQueryAsync();
        return tenantId;
    }

    [Fact]
    public async Task GetOrProvisionUserAsync_UserMissing_CreatesNewUser_ScopedToTenant()
    {
        var slug = "client-" + Guid.NewGuid().ToString("N")[..8];
        var tenantId = await SeedTenantAsync(slug);
        await using var db = fixture.CreateDbContext();
        var accessor = CreateAccessor(db);

        var userId = await accessor.GetOrProvisionUserAsync(
            tenantId, slug, "kc-user-1", "a@example.com", "Ada", "Lovelace");

        userId.ShouldNotBeNull();
        await using var assertDb = fixture.CreateSuperuserDbContext();
        var entity = await assertDb.Users.FirstAsync(u => u.Id == userId);
        entity.TenantId.ShouldBe(tenantId);
        entity.Email.ShouldBe("a@example.com");
    }

    [Fact]
    public async Task GetOrProvisionUserAsync_UserAlreadyExists_ReturnsExistingId_DoesNotDuplicate()
    {
        var slug = "client-" + Guid.NewGuid().ToString("N")[..8];
        var tenantId = await SeedTenantAsync(slug);
        await using var db = fixture.CreateDbContext();
        var accessor = CreateAccessor(db);

        var firstId = await accessor.GetOrProvisionUserAsync(tenantId, slug, "kc-user-1", "a@example.com", "Ada", "Lovelace");
        var secondId = await accessor.GetOrProvisionUserAsync(tenantId, slug, "kc-user-1", "a@example.com", "Ada", "Lovelace");

        secondId.ShouldBe(firstId);
        await using var assertDb = fixture.CreateSuperuserDbContext();
        var count = await assertDb.Users.CountAsync(u => u.TenantId == tenantId && u.UniqueId == "kc-user-1");
        count.ShouldBe(1);
    }

    [Fact]
    public async Task GetOrProvisionUserAsync_DifferentTenantsSameUniqueId_CreatesSeparateUsers()
    {
        var slugA = "client-a-" + Guid.NewGuid().ToString("N")[..8];
        var slugB = "client-b-" + Guid.NewGuid().ToString("N")[..8];
        var tenantA = await SeedTenantAsync(slugA);
        var tenantB = await SeedTenantAsync(slugB);
        await using var db = fixture.CreateDbContext();
        var accessor = CreateAccessor(db);

        var idInA = await accessor.GetOrProvisionUserAsync(tenantA, slugA, "kc-user-1", "a@example.com", "Ada", "Lovelace");
        var idInB = await accessor.GetOrProvisionUserAsync(tenantB, slugB, "kc-user-1", "a@example.com", "Ada", "Lovelace");

        idInA.ShouldNotBe(idInB);
    }

    [Fact]
    public async Task SyncUserAsync_UserMissing_NotDeleted_InsertsNewUser()
    {
        var tenantId = await SeedTenantAsync("client-" + Guid.NewGuid().ToString("N")[..8]);
        await using var db = fixture.CreateDbContext();
        var accessor = CreateAccessor(db);

        await accessor.SyncUserAsync(tenantId, "kc-user-1", "a@example.com", "Ada", "Lovelace", isDeleted: false);

        await using var assertDb = fixture.CreateSuperuserDbContext();
        var entity = await assertDb.Users.FirstOrDefaultAsync(u => u.TenantId == tenantId && u.UniqueId == "kc-user-1");
        entity.ShouldNotBeNull();
        entity.Email.ShouldBe("a@example.com");
    }

    [Fact]
    public async Task SyncUserAsync_UserMissing_IsDeletedTrue_IsNoOp()
    {
        var tenantId = await SeedTenantAsync("client-" + Guid.NewGuid().ToString("N")[..8]);
        await using var db = fixture.CreateDbContext();
        var accessor = CreateAccessor(db);

        await accessor.SyncUserAsync(tenantId, "kc-user-1", "a@example.com", "Ada", "Lovelace", isDeleted: true);

        await using var assertDb = fixture.CreateSuperuserDbContext();
        (await assertDb.Users.AnyAsync(u => u.TenantId == tenantId && u.UniqueId == "kc-user-1")).ShouldBeFalse();
    }

    [Fact]
    public async Task SyncUserAsync_UserExists_IsDeletedTrue_SoftDeletesAndDeactivates()
    {
        var tenantId = await SeedTenantAsync("client-" + Guid.NewGuid().ToString("N")[..8]);
        await using var db = fixture.CreateDbContext();
        var accessor = CreateAccessor(db);
        await accessor.SyncUserAsync(tenantId, "kc-user-1", "a@example.com", "Ada", "Lovelace", isDeleted: false);

        await accessor.SyncUserAsync(tenantId, "kc-user-1", null, null, null, isDeleted: true);

        await using var assertDb = fixture.CreateSuperuserDbContext();
        // IgnoreQueryFilters — UserConfiguration's global filter (DeletedAt == null) would
        // otherwise hide the very row this test is asserting was soft-deleted.
        var entity = await assertDb.Users.IgnoreQueryFilters()
            .FirstAsync(u => u.TenantId == tenantId && u.UniqueId == "kc-user-1");
        entity.IsActive.ShouldBeFalse();
        entity.DeletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task SyncUserAsync_UserExists_NotDeleted_UpdatesProvidedFieldsOnly()
    {
        var tenantId = await SeedTenantAsync("client-" + Guid.NewGuid().ToString("N")[..8]);
        await using var db = fixture.CreateDbContext();
        var accessor = CreateAccessor(db);
        await accessor.SyncUserAsync(tenantId, "kc-user-1", "a@example.com", "Ada", "Lovelace", isDeleted: false);

        await accessor.SyncUserAsync(tenantId, "kc-user-1", email: "new@example.com", firstName: null, lastName: null, isDeleted: false);

        await using var assertDb = fixture.CreateSuperuserDbContext();
        var entity = await assertDb.Users.FirstAsync(u => u.TenantId == tenantId && u.UniqueId == "kc-user-1");
        entity.Email.ShouldBe("new@example.com");
        entity.FirstName.ShouldBe("Ada"); // unchanged, null was passed
        entity.LastName.ShouldBe("Lovelace"); // unchanged
    }
}
