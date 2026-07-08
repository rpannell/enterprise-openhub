using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.Accessor.ElasticIndex.Interfaces;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.ProductManager.Contracts;
using OpenHub.ProductManager.ElasticProcessor.Accessors.Interfaces;
using OpenHub.ProductManager.ElasticProcessor.Accessors.Models;
using OpenHub.ProductManager.ElasticProcessor.Managers;
using OpenHub.ProductManager.ElasticProcessor.Managers.Engines.Interfaces;

namespace OpenHub.ProductManager.ElasticProcessor.Tests.Unit;

public class ProductSyncManagerTests
{
    private readonly IKeyCloakM2MTokenAccessor _m2mAccessor = Substitute.For<IKeyCloakM2MTokenAccessor>();
    private readonly IProductManagerApiAccessor _productApiAccessor = Substitute.For<IProductManagerApiAccessor>();
    private readonly IElasticIndexAccessor<ProductDocument> _productIndexAccessor = Substitute.For<IElasticIndexAccessor<ProductDocument>>();
    private readonly IProductSyncEngine _engine = Substitute.For<IProductSyncEngine>();
    private readonly ProductSyncManager _manager;

    public ProductSyncManagerTests()
    {
        _manager = new ProductSyncManager(
            _m2mAccessor, _productApiAccessor, _productIndexAccessor, _engine,
            Substitute.For<ILogger<ProductSyncManager>>());
    }

    [Fact]
    public async Task HandleAsync_HappyPath_EnsuresTenantThenIndexesDocument()
    {
        var evt = new ProductChangedEvent { ProductId = Guid.NewGuid(), TenantId = Guid.NewGuid(), RealmSlug = "client-a" };
        var product = new ProductApiResponse { Id = evt.ProductId, Sku = "SKU-1", Name = "Widget" };
        var document = new ProductDocument { Id = evt.ProductId, TenantId = evt.TenantId };

        _engine.ShouldFetchProduct(evt).Returns(true);
        _m2mAccessor.GetTokenAsync(evt.RealmSlug, Arg.Any<CancellationToken>()).Returns("token");
        _productApiAccessor.GetProductByIdAsync(evt.ProductId, "token", Arg.Any<CancellationToken>()).Returns(product);
        _engine.BuildDocument(product, evt).Returns(document);

        await _manager.HandleAsync(evt);

        Received.InOrder(() =>
        {
            _productIndexAccessor.EnsureTenantProvisionedAsync(evt.TenantId, Arg.Any<CancellationToken>());
            _productIndexAccessor.IndexDocumentAsync(document, Arg.Any<CancellationToken>());
        });
        await _productIndexAccessor.DidNotReceive().DeleteDocumentAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ProductNotFoundViaApi_DoesNotTouchIndex()
    {
        var evt = new ProductChangedEvent { ProductId = Guid.NewGuid(), TenantId = Guid.NewGuid(), RealmSlug = "client-a" };

        _engine.ShouldFetchProduct(evt).Returns(true);
        _m2mAccessor.GetTokenAsync(evt.RealmSlug, Arg.Any<CancellationToken>()).Returns("token");
        _productApiAccessor.GetProductByIdAsync(evt.ProductId, "token", Arg.Any<CancellationToken>())
            .Returns((ProductApiResponse?)null);

        await _manager.HandleAsync(evt);

        await _productIndexAccessor.DidNotReceive().EnsureTenantProvisionedAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _productIndexAccessor.DidNotReceive().IndexDocumentAsync(Arg.Any<ProductDocument>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_EventIsDeleted_DeletesFromIndexAndSkipsFetch()
    {
        var evt = new ProductChangedEvent { ProductId = Guid.NewGuid(), TenantId = Guid.NewGuid(), IsDeleted = true };

        _engine.ShouldFetchProduct(evt).Returns(false);

        await _manager.HandleAsync(evt);

        await _productIndexAccessor.Received(1).DeleteDocumentAsync(evt.TenantId, evt.ProductId, Arg.Any<CancellationToken>());
        await _productApiAccessor.DidNotReceive().GetProductByIdAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _productIndexAccessor.DidNotReceive().IndexDocumentAsync(Arg.Any<ProductDocument>(), Arg.Any<CancellationToken>());
    }
}
