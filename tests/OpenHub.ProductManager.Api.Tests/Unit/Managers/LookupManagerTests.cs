using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.ProductManager.Api.Accessors.Interfaces;
using OpenHub.ProductManager.Api.Accessors.Models;
using OpenHub.ProductManager.Api.Managers;
using OpenHub.ProductManager.Api.Mappers;

namespace OpenHub.ProductManager.Api.Tests.Unit.Managers;

public class LookupManagerTests
{
    [Fact]
    public async Task GetAllAsync_AggregatesAllSevenLookupSources_IntoOneDto()
    {
        var lookupCache = Substitute.For<ILookupCacheAccessor>();
        var catalogLookup = Substitute.For<ICatalogLookupAccessor>();

        var conditionType = new LookupItemRow(Guid.NewGuid(), "NEW", "New");
        var availabilityStatus = new LookupItemRow(Guid.NewGuid(), "IN_STOCK", "In Stock");
        var uom = new UomLookupRow(Guid.NewGuid(), "KG", "Kilogram", "kg", "weight");
        var currency = new CurrencyLookupRow(Guid.NewGuid(), "USD", "US Dollar", "$");
        var priceType = new LookupItemRow(Guid.NewGuid(), "RETAIL", "Retail");
        var manufacturer = new ManufacturerEntity { Id = Guid.NewGuid(), Name = "Acme", TenantId = Guid.NewGuid(), IsActive = true };
        var brand = new BrandEntity { Id = Guid.NewGuid(), Name = "AcmeBrand", TenantId = Guid.NewGuid(), IsActive = true };

        lookupCache.GetConditionTypesAsync(Arg.Any<CancellationToken>()).Returns(new List<LookupItemRow> { conditionType });
        lookupCache.GetAvailabilityStatusesAsync(Arg.Any<CancellationToken>()).Returns(new List<LookupItemRow> { availabilityStatus });
        lookupCache.GetUnitsOfMeasureAsync(Arg.Any<CancellationToken>()).Returns(new List<UomLookupRow> { uom });
        lookupCache.GetCurrenciesAsync(Arg.Any<CancellationToken>()).Returns(new List<CurrencyLookupRow> { currency });
        lookupCache.GetPriceTypesAsync(Arg.Any<CancellationToken>()).Returns(new List<LookupItemRow> { priceType });
        catalogLookup.GetManufacturersAsync(Arg.Any<CancellationToken>()).Returns(new List<ManufacturerEntity> { manufacturer });
        catalogLookup.GetBrandsAsync(Arg.Any<CancellationToken>()).Returns(new List<BrandEntity> { brand });

        var manager = new LookupManager(lookupCache, catalogLookup, new LookupMapper());

        var result = await manager.GetAllAsync();

        result.ConditionTypes.ShouldHaveSingleItem();
        result.ConditionTypes[0].Name.ShouldBe("New");
        result.AvailabilityStatuses.ShouldHaveSingleItem();
        result.UnitsOfMeasure.ShouldHaveSingleItem();
        result.Currencies.ShouldHaveSingleItem();
        result.PriceTypes.ShouldHaveSingleItem();
        result.Manufacturers.ShouldHaveSingleItem();
        result.Manufacturers[0].Name.ShouldBe("Acme");
        result.Brands.ShouldHaveSingleItem();
        result.Brands[0].Name.ShouldBe("AcmeBrand");
    }
}
