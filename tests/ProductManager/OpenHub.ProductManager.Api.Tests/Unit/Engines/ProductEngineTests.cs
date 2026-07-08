using Shouldly;
using Xunit;
using OpenHub.ProductManager.Api.Managers.Engines;
using OpenHub.ProductManager.Api.Managers.Models;

namespace OpenHub.ProductManager.Api.Tests.Unit.Engines;

public class ProductEngineTests
{
    private static readonly Guid ManufacturerId = Guid.NewGuid();
    private static readonly Guid BrandId = Guid.NewGuid();
    private static readonly Guid ConditionTypeId = Guid.NewGuid();
    private static readonly Guid AvailabilityStatusId = Guid.NewGuid();
    private static readonly Guid WeightUomId = Guid.NewGuid();

    private static ProductDto MakeDto(bool withFks) => new()
    {
        Id = Guid.NewGuid(),
        Sku = "SKU-1",
        Name = "Widget",
        ManufacturerId = withFks ? ManufacturerId : null,
        BrandId = withFks ? BrandId : null,
        ConditionTypeId = withFks ? ConditionTypeId : null,
        AvailabilityStatusId = withFks ? AvailabilityStatusId : null,
        WeightUomId = withFks ? WeightUomId : null,
    };

    private static (
        IReadOnlyDictionary<Guid, string> Manufacturers,
        IReadOnlyDictionary<Guid, string> Brands,
        IReadOnlyDictionary<Guid, string> Conditions,
        IReadOnlyDictionary<Guid, string> Statuses,
        IReadOnlyDictionary<Guid, string> Uoms) FullLookups() => (
            new Dictionary<Guid, string> { [ManufacturerId] = "Acme" },
            new Dictionary<Guid, string> { [BrandId] = "Acme Brand" },
            new Dictionary<Guid, string> { [ConditionTypeId] = "New" },
            new Dictionary<Guid, string> { [AvailabilityStatusId] = "In Stock" },
            new Dictionary<Guid, string> { [WeightUomId] = "kg" });

    [Fact]
    public void Hydrate_AllFksPresentAndResolvable_PopulatesAllDenormalizedNames()
    {
        var engine = new ProductEngine();
        var dto = MakeDto(withFks: true);
        var (manufacturers, brands, conditions, statuses, uoms) = FullLookups();

        var result = engine.Hydrate(dto, manufacturers, brands, conditions, statuses, uoms);

        result.ManufacturerName.ShouldBe("Acme");
        result.BrandName.ShouldBe("Acme Brand");
        result.ConditionTypeName.ShouldBe("New");
        result.AvailabilityStatusName.ShouldBe("In Stock");
        result.WeightUomCode.ShouldBe("kg");
    }

    [Fact]
    public void Hydrate_AllFksNull_LeavesAllDenormalizedNamesNull()
    {
        var engine = new ProductEngine();
        var dto = MakeDto(withFks: false);
        var (manufacturers, brands, conditions, statuses, uoms) = FullLookups();

        var result = engine.Hydrate(dto, manufacturers, brands, conditions, statuses, uoms);

        result.ManufacturerName.ShouldBeNull();
        result.BrandName.ShouldBeNull();
        result.ConditionTypeName.ShouldBeNull();
        result.AvailabilityStatusName.ShouldBeNull();
        result.WeightUomCode.ShouldBeNull();
    }

    [Fact]
    public void Hydrate_FkPresentButMissingFromLookup_LeavesNameNull_DoesNotThrow()
    {
        var engine = new ProductEngine();
        var dto = MakeDto(withFks: true);
        var emptyLookups = new Dictionary<Guid, string>();

        var result = engine.Hydrate(dto, emptyLookups, emptyLookups, emptyLookups, emptyLookups, emptyLookups);

        result.ManufacturerName.ShouldBeNull();
        result.BrandName.ShouldBeNull();
        result.ConditionTypeName.ShouldBeNull();
        result.AvailabilityStatusName.ShouldBeNull();
        result.WeightUomCode.ShouldBeNull();
    }

    [Fact]
    public void Hydrate_PartialFks_OnlyPopulatesTheOnesPresent()
    {
        var engine = new ProductEngine();
        var dto = MakeDto(withFks: false);
        dto.ManufacturerId = ManufacturerId; // only this FK is set
        var (manufacturers, brands, conditions, statuses, uoms) = FullLookups();

        var result = engine.Hydrate(dto, manufacturers, brands, conditions, statuses, uoms);

        result.ManufacturerName.ShouldBe("Acme");
        result.BrandName.ShouldBeNull();
        result.ConditionTypeName.ShouldBeNull();
        result.AvailabilityStatusName.ShouldBeNull();
        result.WeightUomCode.ShouldBeNull();
    }

    [Fact]
    public void HydrateAll_EmptyCollection_ReturnsEmptyList()
    {
        var engine = new ProductEngine();
        var (manufacturers, brands, conditions, statuses, uoms) = FullLookups();

        var result = engine.HydrateAll([], manufacturers, brands, conditions, statuses, uoms);

        result.ShouldBeEmpty();
    }

    [Fact]
    public void HydrateAll_MultipleItems_HydratesEachIndependently()
    {
        var engine = new ProductEngine();
        var dtos = new[] { MakeDto(withFks: true), MakeDto(withFks: false) };
        var (manufacturers, brands, conditions, statuses, uoms) = FullLookups();

        var result = engine.HydrateAll(dtos, manufacturers, brands, conditions, statuses, uoms);

        result.Count.ShouldBe(2);
        result[0].ManufacturerName.ShouldBe("Acme");
        result[1].ManufacturerName.ShouldBeNull();
    }
}
