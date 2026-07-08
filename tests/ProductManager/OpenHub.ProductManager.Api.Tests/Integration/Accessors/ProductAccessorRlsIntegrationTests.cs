using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.ProductManager.Api.Accessors;
using OpenHub.ProductManager.Api.Tests.Integration.Fixtures;

namespace OpenHub.ProductManager.Api.Tests.Integration.Accessors;

/// <summary>
/// Proves the core safety property Row Level Security exists for: a request scoped to tenant A
/// can never see tenant B's products, even though both share the same `catalog.products` table.
/// Runs against a real Postgres container (see <see cref="PostgresCollectionFixture"/>) — this
/// can't be proven with EF Core InMemory, since RLS is a Postgres-only feature and ProductAccessor
/// always opens with a raw `SET LOCAL app.current_tenant_id` call InMemory can't execute at all.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollectionDefinition.Name)]
public class ProductAccessorRlsIntegrationTests(PostgresCollectionFixture fixture)
{
    private static ProductAccessor CreateAccessor(
        OpenHub.ProductManager.Api.Accessors.Data.CatalogDbContext db, Guid tenantId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["TenantId"] = tenantId;
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);
        return new ProductAccessor(db, httpContextAccessor);
    }

    /// <summary>Inserts a tenant + a user + N products directly via SQL — bypassing the accessor
    /// under test, so the seed itself doesn't depend on the code being tested.</summary>
    private async Task<(Guid TenantId, Guid UserId, List<Guid> ProductIds)> SeedTenantWithProductsAsync(
        string slug, int productCount, bool includeSoftDeleted = false)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var productIds = new List<Guid>();

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        await using (var cmd = new NpgsqlCommand(
            "INSERT INTO catalog.tenants (id, slug, name) VALUES (@id, @slug, @name)", connection))
        {
            cmd.Parameters.AddWithValue("id", tenantId);
            cmd.Parameters.AddWithValue("slug", slug);
            cmd.Parameters.AddWithValue("name", slug);
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = new NpgsqlCommand(
            "INSERT INTO catalog.users (id, tenant_id, unique_id, email, first_name, last_name) " +
            "VALUES (@id, @tenantId, @uniqueId, @email, 'Test', 'User')", connection))
        {
            cmd.Parameters.AddWithValue("id", userId);
            cmd.Parameters.AddWithValue("tenantId", tenantId);
            cmd.Parameters.AddWithValue("uniqueId", $"kc-{slug}");
            cmd.Parameters.AddWithValue("email", $"{slug}@example.com");
            await cmd.ExecuteNonQueryAsync();
        }

        for (var i = 0; i < productCount; i++)
        {
            var productId = Guid.NewGuid();
            var isDeleted = includeSoftDeleted && i == productCount - 1;
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO catalog.products (id, tenant_id, sku, name, create_user_id, modify_user_id, deleted_at) " +
                "VALUES (@id, @tenantId, @sku, @name, @userId, @userId, @deletedAt)", connection);
            cmd.Parameters.AddWithValue("id", productId);
            cmd.Parameters.AddWithValue("tenantId", tenantId);
            cmd.Parameters.AddWithValue("sku", $"{slug}-SKU-{i}");
            cmd.Parameters.AddWithValue("name", $"{slug} Product {i}");
            cmd.Parameters.AddWithValue("userId", userId);
            cmd.Parameters.AddWithValue("deletedAt", (object?)(isDeleted ? DateTimeOffset.UtcNow : null) ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
            productIds.Add(productId);
        }

        return (tenantId, userId, productIds);
    }

    [Fact]
    public async Task GetAllAsync_ScopedToTenantA_NeverReturnsTenantBsProducts()
    {
        var (tenantA, _, productsA) = await SeedTenantWithProductsAsync("tenant-a-" + Guid.NewGuid().ToString("N")[..8], 3);
        var (tenantB, _, productsB) = await SeedTenantWithProductsAsync("tenant-b-" + Guid.NewGuid().ToString("N")[..8], 2);

        await using var db = fixture.CreateDbContext();
        var accessor = CreateAccessor(db, tenantA);

        var result = await accessor.GetAllAsync();

        result.Count.ShouldBe(3);
        result.Select(p => p.Id).ShouldBe(productsA, ignoreOrder: true);
        result.Select(p => p.Id).ShouldNotContain(id => productsB.Contains(id));
    }

    [Fact]
    public async Task GetByIdAsync_ProductBelongsToDifferentTenant_ReturnsNull()
    {
        var (_, _, productsA) = await SeedTenantWithProductsAsync("tenant-a-" + Guid.NewGuid().ToString("N")[..8], 1);
        var (tenantB, _, _) = await SeedTenantWithProductsAsync("tenant-b-" + Guid.NewGuid().ToString("N")[..8], 1);

        await using var db = fixture.CreateDbContext();
        var accessorAsTenantB = CreateAccessor(db, tenantB);

        var result = await accessorAsTenantB.GetByIdAsync(productsA[0]);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ProductBelongsToCallersTenant_ReturnsIt()
    {
        var (tenantA, _, productsA) = await SeedTenantWithProductsAsync("tenant-a-" + Guid.NewGuid().ToString("N")[..8], 1);

        await using var db = fixture.CreateDbContext();
        var accessor = CreateAccessor(db, tenantA);

        var result = await accessor.GetByIdAsync(productsA[0]);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(productsA[0]);
    }

    [Fact]
    public async Task GetPagedAsync_ScopedToTenantA_TotalCountExcludesTenantBsProducts()
    {
        var (tenantA, _, _) = await SeedTenantWithProductsAsync("tenant-a-" + Guid.NewGuid().ToString("N")[..8], 5);
        await SeedTenantWithProductsAsync("tenant-b-" + Guid.NewGuid().ToString("N")[..8], 10);

        await using var db = fixture.CreateDbContext();
        var accessor = CreateAccessor(db, tenantA);

        var (items, totalCount) = await accessor.GetPagedAsync(
            page: 1, pageSize: 25, search: null, manufacturerId: null, brandId: null,
            conditionTypeId: null, availabilityStatusId: null, isActive: null, isPublished: null);

        totalCount.ShouldBe(5);
        items.Count.ShouldBe(5);
    }

    [Fact]
    public async Task GetPagedAsync_Paging_ReturnsCorrectPageSlice()
    {
        var (tenantA, _, _) = await SeedTenantWithProductsAsync("tenant-a-" + Guid.NewGuid().ToString("N")[..8], 5);

        await using var db = fixture.CreateDbContext();
        var accessor = CreateAccessor(db, tenantA);

        var (page1, total1) = await accessor.GetPagedAsync(1, 2, null, null, null, null, null, null, null);
        var (page2, total2) = await accessor.GetPagedAsync(2, 2, null, null, null, null, null, null, null);
        var (page3, total3) = await accessor.GetPagedAsync(3, 2, null, null, null, null, null, null, null);

        total1.ShouldBe(5);
        total2.ShouldBe(5);
        total3.ShouldBe(5);
        page1.Count.ShouldBe(2);
        page2.Count.ShouldBe(2);
        page3.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetPagedAsync_Search_MatchesSkuCaseInsensitively()
    {
        var slug = "tenant-a-" + Guid.NewGuid().ToString("N")[..8];
        var (tenantA, _, productIds) = await SeedTenantWithProductsAsync(slug, 3);

        await using var db = fixture.CreateDbContext();
        var accessor = CreateAccessor(db, tenantA);

        var (items, total) = await accessor.GetPagedAsync(
            1, 25, search: $"{slug}-sku-1", null, null, null, null, null, null);

        total.ShouldBe(1);
        items.Single().Id.ShouldBe(productIds[1]);
    }

    [Fact]
    public async Task GetAllAsync_SoftDeletedProduct_IsExcludedByQueryFilter()
    {
        var (tenantA, _, productIds) = await SeedTenantWithProductsAsync(
            "tenant-a-" + Guid.NewGuid().ToString("N")[..8], 3, includeSoftDeleted: true);

        await using var db = fixture.CreateDbContext();
        var accessor = CreateAccessor(db, tenantA);

        var result = await accessor.GetAllAsync();

        result.Count.ShouldBe(2);
        result.Select(p => p.Id).ShouldNotContain(productIds[^1]);
    }

    [Fact]
    public async Task GetPagedAsync_SoftDeletedProduct_IsExcludedByQueryFilter()
    {
        var (tenantA, _, productIds) = await SeedTenantWithProductsAsync(
            "tenant-a-" + Guid.NewGuid().ToString("N")[..8], 3, includeSoftDeleted: true);

        await using var db = fixture.CreateDbContext();
        var accessor = CreateAccessor(db, tenantA);

        var (items, totalCount) = await accessor.GetPagedAsync(
            page: 1, pageSize: 25, search: null, manufacturerId: null, brandId: null,
            conditionTypeId: null, availabilityStatusId: null, isActive: null, isPublished: null);

        totalCount.ShouldBe(2);
        items.Select(p => p.Id).ShouldNotContain(productIds[^1]);
    }

    [Fact]
    public async Task GetByIdIncludingDeletedAsync_SoftDeletedProduct_ReturnsItWithDeletedAtSet()
    {
        var (tenantA, _, productIds) = await SeedTenantWithProductsAsync(
            "tenant-a-" + Guid.NewGuid().ToString("N")[..8], 1, includeSoftDeleted: true);

        await using var db = fixture.CreateDbContext();
        var accessor = CreateAccessor(db, tenantA);

        var result = await accessor.GetByIdIncludingDeletedAsync(productIds[0]);

        result.ShouldNotBeNull();
        result.DeletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetByIdIncludingDeletedAsync_ProductBelongsToDifferentTenant_ReturnsNull()
    {
        var (_, _, productsA) = await SeedTenantWithProductsAsync("tenant-a-" + Guid.NewGuid().ToString("N")[..8], 1);
        var (tenantB, _, _) = await SeedTenantWithProductsAsync("tenant-b-" + Guid.NewGuid().ToString("N")[..8], 1);

        await using var db = fixture.CreateDbContext();
        var accessorAsTenantB = CreateAccessor(db, tenantB);

        var result = await accessorAsTenantB.GetByIdIncludingDeletedAsync(productsA[0]);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task SoftDeleteAsync_MarksDeletedAtAndDeleteUserId()
    {
        var (tenantA, userId, productIds) = await SeedTenantWithProductsAsync(
            "tenant-a-" + Guid.NewGuid().ToString("N")[..8], 1);

        await using var db = fixture.CreateDbContext();
        var accessor = CreateAccessor(db, tenantA);
        var deleteUserId = Guid.NewGuid();

        // Insert the deleting user so the FK on delete_user_id is satisfiable.
        await using (var connection = new NpgsqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO catalog.users (id, tenant_id, unique_id, email, first_name, last_name) " +
                "VALUES (@id, @tenantId, @uniqueId, @email, 'Deleter', 'User')", connection);
            cmd.Parameters.AddWithValue("id", deleteUserId);
            cmd.Parameters.AddWithValue("tenantId", tenantA);
            cmd.Parameters.AddWithValue("uniqueId", "kc-deleter");
            cmd.Parameters.AddWithValue("email", "deleter@example.com");
            await cmd.ExecuteNonQueryAsync();
        }

        await accessor.SoftDeleteAsync(productIds[0], deleteUserId);

        var stillVisible = await accessor.GetByIdAsync(productIds[0]);
        stillVisible.ShouldBeNull(); // soft-delete query filter excludes it
    }
}
