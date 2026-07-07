using Microsoft.AspNetCore.Http;
using Npgsql;
using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.ProductManager.Api.Accessors;
using OpenHub.ProductManager.Api.Accessors.Models;
using OpenHub.ProductManager.Api.Tests.Integration.Fixtures;

namespace OpenHub.ProductManager.Api.Tests.Integration.Accessors;

/// <summary>
/// ProductPriceAccessor is RLS-scoped like ProductAccessor — see
/// ProductAccessorRlsIntegrationTests' class doc for why this needs real Postgres.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollectionDefinition.Name)]
public class ProductPriceAccessorIntegrationTests(PostgresCollectionFixture fixture)
{
    private static ProductPriceAccessor CreateAccessor(
        OpenHub.ProductManager.Api.Accessors.Data.CatalogDbContext db, Guid tenantId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["TenantId"] = tenantId;
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);
        return new ProductPriceAccessor(db, httpContextAccessor);
    }

    private async Task<(Guid PriceTypeId, Guid CurrencyId)> GetSeededRefIdsAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        async Task<Guid> GetOneAsync(string sql)
        {
            await using var cmd = new NpgsqlCommand(sql, connection);
            return (Guid)(await cmd.ExecuteScalarAsync())!;
        }

        var priceTypeId = await GetOneAsync("SELECT id FROM ref.price_types WHERE code = 'RETAIL'");
        var currencyId = await GetOneAsync("SELECT id FROM ref.currencies WHERE code = 'USD'");
        return (priceTypeId, currencyId);
    }

    /// <summary>Inserts a tenant + user + one product directly via SQL, bypassing accessors under test.</summary>
    private async Task<(Guid TenantId, Guid ProductId)> SeedTenantWithProductAsync(string slug)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var productId = Guid.NewGuid();

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

        await using (var cmd = new NpgsqlCommand(
            "INSERT INTO catalog.products (id, tenant_id, sku, name, create_user_id, modify_user_id) " +
            "VALUES (@id, @tenantId, @sku, @name, @userId, @userId)", connection))
        {
            cmd.Parameters.AddWithValue("id", productId);
            cmd.Parameters.AddWithValue("tenantId", tenantId);
            cmd.Parameters.AddWithValue("sku", $"{slug}-SKU-1");
            cmd.Parameters.AddWithValue("name", $"{slug} Product");
            cmd.Parameters.AddWithValue("userId", userId);
            await cmd.ExecuteNonQueryAsync();
        }

        return (tenantId, productId);
    }

    private ProductPriceEntity MakePrice(
        Guid tenantId, Guid productId, Guid priceTypeId, Guid currencyId, bool isActive = true) => new()
    {
        Id = Guid.Empty, // SaveAsync treats Guid.Empty as "insert"
        TenantId = tenantId,
        ProductId = productId,
        PriceTypeId = priceTypeId,
        CurrencyId = currencyId,
        Amount = 19.99m,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
        EffectiveUntil = null,
        IsActive = isActive,
    };

    [Fact]
    public async Task SaveAsync_NewPrice_ThenGetActiveByProductIdAsync_ReturnsIt()
    {
        var (tenantId, productId) = await SeedTenantWithProductAsync("tenant-" + Guid.NewGuid().ToString("N")[..8]);
        var (priceTypeId, currencyId) = await GetSeededRefIdsAsync();
        await using var db = fixture.CreateDbContext();
        var accessor = CreateAccessor(db, tenantId);

        await accessor.SaveAsync(MakePrice(tenantId, productId, priceTypeId, currencyId));
        var prices = await accessor.GetActiveByProductIdAsync(productId);

        prices.ShouldHaveSingleItem();
        prices[0].ProductId.ShouldBe(productId);
    }

    [Fact]
    public async Task GetActiveByProductIdAsync_ExcludesInactivePrices()
    {
        var (tenantId, productId) = await SeedTenantWithProductAsync("tenant-" + Guid.NewGuid().ToString("N")[..8]);
        var (priceTypeId, currencyId) = await GetSeededRefIdsAsync();
        await using var db = fixture.CreateDbContext();
        var accessor = CreateAccessor(db, tenantId);

        await accessor.SaveAsync(MakePrice(tenantId, productId, priceTypeId, currencyId, isActive: true));
        await accessor.SaveAsync(MakePrice(tenantId, productId, priceTypeId, currencyId, isActive: false));

        var prices = await accessor.GetActiveByProductIdAsync(productId);

        prices.ShouldHaveSingleItem();
        prices[0].IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task GetActiveByProductIdAsync_ScopedToDifferentTenant_ReturnsEmpty()
    {
        var (tenantA, productA) = await SeedTenantWithProductAsync("tenant-a-" + Guid.NewGuid().ToString("N")[..8]);
        var (tenantB, _) = await SeedTenantWithProductAsync("tenant-b-" + Guid.NewGuid().ToString("N")[..8]);
        var (priceTypeId, currencyId) = await GetSeededRefIdsAsync();

        await using (var dbA = fixture.CreateDbContext())
        {
            await CreateAccessor(dbA, tenantA).SaveAsync(MakePrice(tenantA, productA, priceTypeId, currencyId));
        }

        // Tenant B's own accessor querying tenant A's product id — RLS scopes by row's own
        // tenant_id, which never matches tenant B's session context, regardless of which
        // product_id is asked for.
        await using var dbB = fixture.CreateDbContext();
        var accessorB = CreateAccessor(dbB, tenantB);

        var prices = await accessorB.GetActiveByProductIdAsync(productA);

        prices.ShouldBeEmpty();
    }
}
