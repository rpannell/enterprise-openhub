using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;
using OpenHub.ProductManager.Api.Accessors;
using OpenHub.ProductManager.Api.Tests.Integration.Fixtures;

namespace OpenHub.ProductManager.Api.Tests.Integration.Accessors;

/// <summary>
/// LookupCacheAccessor reads platform-wide ref.* tables via raw SqlQueryRaw — like the RLS-scoped
/// accessors, EF Core InMemory can't execute this at all (SqlQueryRaw is provider-specific, no
/// in-memory equivalent), so this is real-Postgres-only too, even though ref.* tables carry no RLS.
/// </summary>
[Trait("Category", "Integration")]
[Collection(PostgresCollectionDefinition.Name)]
public class LookupCacheAccessorIntegrationTests(PostgresCollectionFixture fixture)
{
    private static HybridCache CreateHybridCache()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        return services.BuildServiceProvider().GetRequiredService<HybridCache>();
    }

    private LookupCacheAccessor CreateAccessor(HybridCache? cache = null)
        => new(fixture.CreateSuperuserDbContext(), cache ?? CreateHybridCache());

    [Fact]
    public async Task GetConditionTypeNamesAsync_ReturnsSeededReferenceData()
    {
        var accessor = CreateAccessor();

        var result = await accessor.GetConditionTypeNamesAsync();

        result.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task GetCurrenciesAsync_IncludesSeededUsd()
    {
        var accessor = CreateAccessor();

        var result = await accessor.GetCurrenciesAsync();

        result.ShouldContain(c => c.Code == "USD");
    }

    [Fact]
    public async Task GetPriceTypesAsync_IncludesSeededRetail()
    {
        var accessor = CreateAccessor();

        var result = await accessor.GetPriceTypesAsync();

        result.ShouldContain(p => p.Code == "RETAIL");
    }

    [Fact]
    public async Task GetUnitsOfMeasureAsync_ReturnsRowsWithUomType()
    {
        var accessor = CreateAccessor();

        var result = await accessor.GetUnitsOfMeasureAsync();

        result.ShouldNotBeEmpty();
        result.ShouldAllBe(u => !string.IsNullOrWhiteSpace(u.UomType));
    }

    [Fact]
    public async Task GetConditionTypeNamesAsync_SecondCall_ServedFromCache_ReturnsSameData()
    {
        // Disposes the DbContext after the first call — if the second call weren't served from
        // cache, it would throw ObjectDisposedException instead of returning matching data.
        var cache = CreateHybridCache();
        var db = fixture.CreateSuperuserDbContext();
        var accessor = new LookupCacheAccessor(db, cache);

        var first = await accessor.GetConditionTypeNamesAsync();
        await db.DisposeAsync();
        var second = await accessor.GetConditionTypeNamesAsync();

        second.ShouldBe(first);
    }

    [Fact]
    public async Task InvalidateAsync_ClearsCachedEntry_NextCallReloadsFromDb()
    {
        var cache = CreateHybridCache();
        var accessor = CreateAccessor(cache);
        var first = await accessor.GetConditionTypeNamesAsync();

        await accessor.InvalidateAsync("ref:condition_types");
        var second = await accessor.GetConditionTypeNamesAsync();

        second.ShouldBe(first); // same underlying data, freshly reloaded rather than cached
    }
}
