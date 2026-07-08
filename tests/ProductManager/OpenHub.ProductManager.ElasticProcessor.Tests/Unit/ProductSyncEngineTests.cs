using Shouldly;
using Xunit;
using OpenHub.ProductManager.Contracts;
using OpenHub.ProductManager.ElasticProcessor.Accessors.Models;
using OpenHub.ProductManager.ElasticProcessor.Managers.Engines;

namespace OpenHub.ProductManager.ElasticProcessor.Tests.Unit;

public class ProductSyncEngineTests
{
    private readonly ProductSyncEngine _engine = new();

    [Fact]
    public void ShouldFetchProduct_ReturnsFalse_WhenEventIsDeleted()
    {
        var evt = new ProductChangedEvent { IsDeleted = true };

        _engine.ShouldFetchProduct(evt).ShouldBeFalse();
    }

    [Fact]
    public void ShouldFetchProduct_ReturnsTrue_WhenEventIsNotDeleted()
    {
        var evt = new ProductChangedEvent { IsDeleted = false };

        _engine.ShouldFetchProduct(evt).ShouldBeTrue();
    }

    [Fact]
    public void BuildDocument_MapsAllFieldsFromProductAndEvent()
    {
        var productId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var product = new ProductApiResponse
        {
            Id = productId,
            Sku = "SKU-1",
            Name = "Widget",
            Description = "A widget",
            IsActive = true,
            IsPublished = false,
        };
        var evt = new ProductChangedEvent { ProductId = productId, TenantId = tenantId };

        var document = _engine.BuildDocument(product, evt);

        document.Id.ShouldBe(productId);
        document.TenantId.ShouldBe(tenantId);
        document.Sku.ShouldBe("SKU-1");
        document.Name.ShouldBe("Widget");
        document.Description.ShouldBe("A widget");
        document.IsActive.ShouldBeTrue();
        document.IsPublished.ShouldBeFalse();
    }

    [Fact]
    public void BuildDocument_PassesThroughNullDescription()
    {
        var product = new ProductApiResponse
        {
            Id = Guid.NewGuid(),
            Sku = "SKU-2",
            Name = "Gadget",
            Description = null,
            IsActive = true,
            IsPublished = true,
        };
        var evt = new ProductChangedEvent { TenantId = Guid.NewGuid() };

        var document = _engine.BuildDocument(product, evt);

        document.Description.ShouldBeNull();
    }
}
