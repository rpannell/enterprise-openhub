using Microsoft.AspNetCore.Http;
using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.Accessor.RabbitMq;
using OpenHub.ProductManager.Api.Accessors.Interfaces;
using OpenHub.ProductManager.Api.Accessors.Models;
using OpenHub.ProductManager.Api.Managers.Engines.Interfaces;
using OpenHub.ProductManager.Api.Managers.Models;
using OpenHub.ProductManager.Api.Mappers;
using OpenHub.ProductManager.Api.Models.Requests;
using OpenHub.ProductManager.Contracts;

namespace OpenHub.ProductManager.Api.Tests.Unit.Managers;

// "ProductManager" is fully qualified everywhere below (rather than aliased/imported) because
// it's ambiguous with the OpenHub.ProductManager.* namespace segment when referenced bare from
// within this OpenHub.ProductManager.Api.Tests... namespace — enclosing-namespace-member lookup
// wins over a using-alias here.
public class ProductManagerTests
{
    private readonly IProductAccessor _productAccessor = Substitute.For<IProductAccessor>();
    private readonly IProductPriceAccessor _priceAccessor = Substitute.For<IProductPriceAccessor>();
    private readonly ILookupCacheAccessor _lookupCache = Substitute.For<ILookupCacheAccessor>();
    private readonly ICatalogLookupAccessor _catalogLookup = Substitute.For<ICatalogLookupAccessor>();
    private readonly IProductEngine _productEngine = Substitute.For<IProductEngine>();
    private readonly IProductPricingEngine _pricingEngine = Substitute.For<IProductPricingEngine>();
    private readonly IMessagePublisher _messagePublisher = Substitute.For<IMessagePublisher>();
    private readonly Guid _tenantId = Guid.NewGuid();
    private const string RealmSlug = "client-a";

    private OpenHub.ProductManager.Api.Managers.ProductManager CreateManager()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Items["TenantId"] = _tenantId;
        httpContext.Items["TenantSlug"] = RealmSlug;
        var httpContextAccessor = Substitute.For<IHttpContextAccessor>();
        httpContextAccessor.HttpContext.Returns(httpContext);

        _lookupCache.GetConditionTypeNamesAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string>());
        _lookupCache.GetAvailabilityStatusNamesAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string>());
        _lookupCache.GetUomCodesAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string>());
        _lookupCache.GetPriceTypeNamesAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string>());
        _lookupCache.GetCurrencyCodesAsync(Arg.Any<CancellationToken>())
            .Returns(new Dictionary<Guid, string>());
        _catalogLookup.GetManufacturersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ManufacturerEntity>());
        _catalogLookup.GetBrandsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<BrandEntity>());

        return new OpenHub.ProductManager.Api.Managers.ProductManager(
            _productAccessor, _priceAccessor, _lookupCache, _catalogLookup,
            _productEngine, _pricingEngine, new ProductMapper(), httpContextAccessor, _messagePublisher);
    }

    private static ProductEntity MakeEntity(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Sku = "SKU-1",
        Name = "Widget",
        IsActive = true,
        IsPublished = false,
        CreateUserId = Guid.NewGuid(),
        ModifyUserId = Guid.NewGuid(),
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task CreateAsync_ManufacturerNotFound_ThrowsKeyNotFoundException()
    {
        var manager = CreateManager();
        var request = new CreateProductRequest { Sku = "SKU-1", Name = "Widget", Manufacturer = "Unknown", Brand = "Whatever" };

        await Should.ThrowAsync<KeyNotFoundException>(() => manager.CreateAsync(request, Guid.NewGuid()));

        await _messagePublisher.DidNotReceive().PublishAsync(Arg.Any<ProductChangedEvent>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_BrandNotFound_ThrowsKeyNotFoundException()
    {
        var manager = CreateManager();
        var manufacturerId = Guid.NewGuid();
        _catalogLookup.GetManufacturersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ManufacturerEntity> { new() { Id = manufacturerId, Name = "Acme", TenantId = _tenantId } });
        var request = new CreateProductRequest { Sku = "SKU-1", Name = "Widget", Manufacturer = "Acme", Brand = "Unknown" };

        await Should.ThrowAsync<KeyNotFoundException>(() => manager.CreateAsync(request, Guid.NewGuid()));
    }

    [Fact]
    public async Task CreateAsync_HappyPath_PublishesProductChangedEvent_WithCorrectFields()
    {
        var manager = CreateManager();

        var manufacturerId = Guid.NewGuid();
        var brandId = Guid.NewGuid();
        _catalogLookup.GetManufacturersAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ManufacturerEntity> { new() { Id = manufacturerId, Name = "Acme", TenantId = _tenantId } });
        _catalogLookup.GetBrandsAsync(Arg.Any<CancellationToken>())
            .Returns(new List<BrandEntity> { new() { Id = brandId, Name = "AcmeBrand", TenantId = _tenantId } });

        var createdEntity = MakeEntity();
        _productAccessor.CreateAsync(Arg.Any<ProductEntity>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<ProductEntity>());
        _productAccessor.CreateAsync(Arg.Any<ProductEntity>(), Arg.Any<CancellationToken>())
            .Returns(createdEntity);

        var userId = Guid.NewGuid();
        var request = new CreateProductRequest { Sku = "SKU-1", Name = "Widget", Manufacturer = "Acme", Brand = "AcmeBrand" };

        var result = await manager.CreateAsync(request, userId);

        result.Id.ShouldBe(createdEntity.Id);
        await _messagePublisher.Received(1).PublishAsync(
            Arg.Is<ProductChangedEvent>(e =>
                e.ProductId == createdEntity.Id &&
                e.TenantId == _tenantId &&
                e.UserId == userId &&
                e.RealmSlug == RealmSlug &&
                e.IsDeleted == false),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_ProductNotFound_ReturnsNull_DoesNotPublish()
    {
        _productAccessor.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ProductEntity?)null);
        var manager = CreateManager();

        var result = await manager.UpdateAsync(Guid.NewGuid(), new UpdateProductRequest(), Guid.NewGuid());

        result.ShouldBeNull();
        await _messagePublisher.DidNotReceive().PublishAsync(Arg.Any<ProductChangedEvent>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_ProductFound_PublishesProductChangedEvent_NotDeleted()
    {
        var existing = MakeEntity();
        _productAccessor.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
        _productAccessor.UpdateAsync(Arg.Any<ProductEntity>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<ProductEntity>());
        var manager = CreateManager();
        var userId = Guid.NewGuid();

        var result = await manager.UpdateAsync(existing.Id, new UpdateProductRequest { Name = "New Name" }, userId);

        result.ShouldNotBeNull();
        result.Name.ShouldBe("New Name");
        await _messagePublisher.Received(1).PublishAsync(
            Arg.Is<ProductChangedEvent>(e => e.ProductId == existing.Id && e.IsDeleted == false && e.UserId == userId),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_ProductNotFound_ReturnsFalse_DoesNotPublish()
    {
        _productAccessor.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ProductEntity?)null);
        var manager = CreateManager();

        var result = await manager.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        result.ShouldBeFalse();
        await _messagePublisher.DidNotReceive().PublishAsync(Arg.Any<ProductChangedEvent>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_ProductFound_SoftDeletes_PublishesEvent_WithIsDeletedTrue()
    {
        var existing = MakeEntity();
        _productAccessor.GetByIdAsync(existing.Id, Arg.Any<CancellationToken>()).Returns(existing);
        var manager = CreateManager();
        var userId = Guid.NewGuid();

        var result = await manager.DeleteAsync(existing.Id, userId);

        result.ShouldBeTrue();
        await _productAccessor.Received(1).SoftDeleteAsync(existing.Id, userId, Arg.Any<CancellationToken>());
        await _messagePublisher.Received(1).PublishAsync(
            Arg.Is<ProductChangedEvent>(e => e.ProductId == existing.Id && e.IsDeleted == true && e.UserId == userId),
            Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_ProductNotFound_ReturnsNull()
    {
        _productAccessor.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ProductEntity?)null);
        var manager = CreateManager();

        var result = await manager.GetByIdAsync(Guid.NewGuid());

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ProductFound_CallsEngineHydrate_AndHydratesPrices()
    {
        var entity = MakeEntity();
        _productAccessor.GetByIdAsync(entity.Id, Arg.Any<CancellationToken>()).Returns(entity);
        _priceAccessor.GetActiveByProductIdAsync(entity.Id, Arg.Any<CancellationToken>())
            .Returns(new List<ProductPriceEntity>());
        _productEngine.Hydrate(
                Arg.Any<ProductDto>(), Arg.Any<IReadOnlyDictionary<Guid, string>>(), Arg.Any<IReadOnlyDictionary<Guid, string>>(),
                Arg.Any<IReadOnlyDictionary<Guid, string>>(), Arg.Any<IReadOnlyDictionary<Guid, string>>(), Arg.Any<IReadOnlyDictionary<Guid, string>>())
            .Returns(ci => ci.Arg<ProductDto>());
        _pricingEngine.HydratePrices(Arg.Any<IEnumerable<ProductPriceDto>>(), Arg.Any<IReadOnlyDictionary<Guid, string>>(), Arg.Any<IReadOnlyDictionary<Guid, string>>())
            .Returns([]);

        var manager = CreateManager();
        var result = await manager.GetByIdAsync(entity.Id);

        result.ShouldNotBeNull();
        _productEngine.Received(1).Hydrate(
            Arg.Any<ProductDto>(), Arg.Any<IReadOnlyDictionary<Guid, string>>(), Arg.Any<IReadOnlyDictionary<Guid, string>>(),
            Arg.Any<IReadOnlyDictionary<Guid, string>>(), Arg.Any<IReadOnlyDictionary<Guid, string>>(), Arg.Any<IReadOnlyDictionary<Guid, string>>());
        _pricingEngine.Received(1).HydratePrices(Arg.Any<IEnumerable<ProductPriceDto>>(), Arg.Any<IReadOnlyDictionary<Guid, string>>(), Arg.Any<IReadOnlyDictionary<Guid, string>>());
    }
}
