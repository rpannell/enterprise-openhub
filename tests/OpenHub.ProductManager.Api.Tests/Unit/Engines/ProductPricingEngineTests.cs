using Shouldly;
using Xunit;
using OpenHub.ProductManager.Api.Managers.Engines;
using OpenHub.ProductManager.Api.Managers.Models;

namespace OpenHub.ProductManager.Api.Tests.Unit.Engines;

public class ProductPricingEngineTests
{
    private static readonly Guid RetailPriceTypeId = Guid.NewGuid();
    private static readonly Guid WholesalePriceTypeId = Guid.NewGuid();
    private static readonly Guid CurrencyId = Guid.NewGuid();

    private static readonly IReadOnlyDictionary<Guid, string> PriceTypeNames = new Dictionary<Guid, string>
    {
        [RetailPriceTypeId] = "retail",
        [WholesalePriceTypeId] = "wholesale",
    };

    private static ProductPriceDto MakePrice(
        DateTimeOffset effectiveFrom, DateTimeOffset? effectiveUntil, bool isActive = true,
        Guid? priceTypeId = null, decimal amount = 10m) => new()
        {
            Id = Guid.NewGuid(),
            PriceTypeId = priceTypeId ?? RetailPriceTypeId,
            CurrencyId = CurrencyId,
            Amount = amount,
            EffectiveFrom = effectiveFrom,
            EffectiveUntil = effectiveUntil,
            IsActive = isActive,
        };

    [Fact]
    public void HydratePrices_PopulatesPriceTypeNameAndCurrencyCode()
    {
        var engine = new ProductPricingEngine();
        var price = MakePrice(DateTimeOffset.UtcNow.AddDays(-1), null);
        var currencies = new Dictionary<Guid, string> { [CurrencyId] = "USD" };

        var result = engine.HydratePrices([price], PriceTypeNames, currencies);

        result.Single().PriceTypeName.ShouldBe("retail");
        result.Single().CurrencyCode.ShouldBe("USD");
    }

    [Fact]
    public void HydratePrices_UnresolvableIds_LeavesNamesNull()
    {
        var engine = new ProductPricingEngine();
        var price = MakePrice(DateTimeOffset.UtcNow.AddDays(-1), null);

        var result = engine.HydratePrices([price], new Dictionary<Guid, string>(), new Dictionary<Guid, string>());

        result.Single().PriceTypeName.ShouldBeNull();
        result.Single().CurrencyCode.ShouldBeNull();
    }

    [Fact]
    public void GetCurrentPrice_NoMatchingPriceType_ReturnsNull()
    {
        var engine = new ProductPricingEngine();
        var now = DateTimeOffset.UtcNow;
        var price = MakePrice(now.AddDays(-1), null, priceTypeId: WholesalePriceTypeId);

        var result = engine.GetCurrentPrice([price], "retail", PriceTypeNames);

        result.ShouldBeNull();
    }

    [Fact]
    public void GetCurrentPrice_InactivePrice_IsExcludedEvenIfWindowMatches()
    {
        var engine = new ProductPricingEngine();
        var now = DateTimeOffset.UtcNow;
        var price = MakePrice(now.AddDays(-1), null, isActive: false);

        var result = engine.GetCurrentPrice([price], "retail", PriceTypeNames);

        result.ShouldBeNull();
    }

    [Fact]
    public void GetCurrentPrice_EffectiveFromExactlyNow_IsIncluded()
    {
        var engine = new ProductPricingEngine();
        var now = DateTimeOffset.UtcNow;
        // EffectiveFrom in the past-but-close-enough to guarantee <= "now" as evaluated inside
        // the engine (which captures its own DateTimeOffset.UtcNow at call time).
        var price = MakePrice(now.AddSeconds(-1), null);

        var result = engine.GetCurrentPrice([price], "retail", PriceTypeNames);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(price.Id);
    }

    [Fact]
    public void GetCurrentPrice_EffectiveFromInTheFuture_IsExcluded()
    {
        var engine = new ProductPricingEngine();
        var now = DateTimeOffset.UtcNow;
        var price = MakePrice(now.AddDays(1), null);

        var result = engine.GetCurrentPrice([price], "retail", PriceTypeNames);

        result.ShouldBeNull();
    }

    [Fact]
    public void GetCurrentPrice_EffectiveUntilNull_IsOpenEndedAndIncluded()
    {
        var engine = new ProductPricingEngine();
        var now = DateTimeOffset.UtcNow;
        var price = MakePrice(now.AddDays(-30), null);

        var result = engine.GetCurrentPrice([price], "retail", PriceTypeNames);

        result.ShouldNotBeNull();
    }

    [Fact]
    public void GetCurrentPrice_EffectiveUntilInThePast_IsExcluded()
    {
        var engine = new ProductPricingEngine();
        var now = DateTimeOffset.UtcNow;
        var price = MakePrice(now.AddDays(-30), now.AddDays(-1));

        var result = engine.GetCurrentPrice([price], "retail", PriceTypeNames);

        result.ShouldBeNull();
    }

    [Fact]
    public void GetCurrentPrice_EffectiveUntilInTheFuture_IsIncluded()
    {
        var engine = new ProductPricingEngine();
        var now = DateTimeOffset.UtcNow;
        var price = MakePrice(now.AddDays(-1), now.AddDays(1));

        var result = engine.GetCurrentPrice([price], "retail", PriceTypeNames);

        result.ShouldNotBeNull();
    }

    [Fact]
    public void GetCurrentPrice_MultipleEligiblePrices_ReturnsTheOneWithLatestEffectiveFrom()
    {
        var engine = new ProductPricingEngine();
        var now = DateTimeOffset.UtcNow;
        var oldest = MakePrice(now.AddDays(-30), null, amount: 5m);
        var middle = MakePrice(now.AddDays(-10), null, amount: 8m);
        var newest = MakePrice(now.AddDays(-1), null, amount: 12m);

        // Shuffle input order to prove this isn't relying on input ordering.
        var result = engine.GetCurrentPrice([middle, oldest, newest], "retail", PriceTypeNames);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(newest.Id);
        result.Amount.ShouldBe(12m);
    }

    [Fact]
    public void GetCurrentPrice_MixOfEligibleAndIneligiblePrices_OnlyConsidersEligibleOnes()
    {
        var engine = new ProductPricingEngine();
        var now = DateTimeOffset.UtcNow;
        var eligible = MakePrice(now.AddDays(-5), null, amount: 20m);
        var wrongType = MakePrice(now.AddDays(-1), null, priceTypeId: WholesalePriceTypeId, amount: 999m);
        var expired = MakePrice(now.AddDays(-10), now.AddDays(-1), amount: 999m);
        var inactive = MakePrice(now.AddDays(-1), null, isActive: false, amount: 999m);

        var result = engine.GetCurrentPrice([eligible, wrongType, expired, inactive], "retail", PriceTypeNames);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(eligible.Id);
    }

    [Fact]
    public void GetCurrentPrice_EmptyPriceList_ReturnsNull()
    {
        var engine = new ProductPricingEngine();

        var result = engine.GetCurrentPrice([], "retail", PriceTypeNames);

        result.ShouldBeNull();
    }
}
