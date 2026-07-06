using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.ProductManager.Contracts;
using OpenHub.ProductManager.ElasticProcessor.Managers.Interfaces;
using OpenHub.ProductManager.ElasticProcessor.Messaging.Handlers;

namespace OpenHub.ProductManager.ElasticProcessor.Tests.Unit;

public class ProductChangedEventHandlerTests
{
    [Fact]
    public async Task Handle_DelegatesToProductSyncManager_WithSameEvent()
    {
        var manager = Substitute.For<IProductSyncManager>();
        var handler = new ProductChangedEventHandler(manager);
        var evt = new ProductChangedEvent
        {
            ProductId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            RealmSlug = "client-a",
        };

        await handler.Handle(evt);

        await manager.Received(1).HandleAsync(evt, Arg.Any<CancellationToken>());
    }
}
