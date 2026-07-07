using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.ProductManager.Api.Accessors;
using OpenHub.ProductManager.Api.Tests.Integration.Fixtures;

namespace OpenHub.ProductManager.Api.Tests.Integration.Accessors;

/// <summary>
/// CatalogLookupAccessor (manufacturers/brands) is RLS-scoped like ProductAccessor — same
/// tenant-isolation shape, see ProductAccessorRlsIntegrationTests' class doc for why this needs
/// real Postgres.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollectionDefinition.Name)]
public class CatalogLookupAccessorIntegrationTests(PostgresCollectionFixture fixture)
{
    private static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private static CatalogLookupAccessor CreateAccessor(
        OpenHub.ProductManager.Api.Accessors.Data.CatalogDbContext db, Guid tenantId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["TenantId"] = tenantId;
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);
        return new CatalogLookupAccessor(db, httpContextAccessor, CreateHybridCache());
    }

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
    public async Task InsertManufacturerAsync_ThenGetManufacturersAsync_ReturnsItScopedToTenant()
    {
        var tenantId = await SeedTenantAsync("client-" + Guid.NewGuid().ToString("N")[..8]);
        await using var db = fixture.CreateDbContext();
        var accessor = CreateAccessor(db, tenantId);

        var inserted = await accessor.InsertManufacturerAsync("Acme", tenantId);
        var all = await accessor.GetManufacturersAsync();

        all.ShouldContain(m => m.Id == inserted.Id && m.Name == "Acme");
    }

    [Fact]
    public async Task GetManufacturersAsync_ScopedToTenantA_NeverReturnsTenantBsManufacturers()
    {
        var tenantA = await SeedTenantAsync("client-a-" + Guid.NewGuid().ToString("N")[..8]);
        var tenantB = await SeedTenantAsync("client-b-" + Guid.NewGuid().ToString("N")[..8]);

        await using (var dbB = fixture.CreateDbContext())
        {
            await CreateAccessor(dbB, tenantB).InsertManufacturerAsync("Tenant B Manufacturer", tenantB);
        }

        await using var dbA = fixture.CreateDbContext();
        var accessorA = CreateAccessor(dbA, tenantA);
        await accessorA.InsertManufacturerAsync("Tenant A Manufacturer", tenantA);

        var resultsForA = await accessorA.GetManufacturersAsync();

        resultsForA.ShouldContain(m => m.Name == "Tenant A Manufacturer");
        resultsForA.ShouldNotContain(m => m.Name == "Tenant B Manufacturer");
    }

    [Fact]
    public async Task InsertBrandAsync_ThenGetBrandsAsync_ReturnsItScopedToTenant()
    {
        var tenantId = await SeedTenantAsync("client-" + Guid.NewGuid().ToString("N")[..8]);
        await using var db = fixture.CreateDbContext();
        var accessor = CreateAccessor(db, tenantId);

        var inserted = await accessor.InsertBrandAsync("AcmeBrand", tenantId);
        var all = await accessor.GetBrandsAsync();

        all.ShouldContain(b => b.Id == inserted.Id && b.Name == "AcmeBrand");
    }
}
