using Rebus.Handlers;
using OpenHub.ProductManager.Contracts;
using OpenHub.ProductManager.ElasticProcessor.Managers.Interfaces;

namespace OpenHub.ProductManager.ElasticProcessor.Messaging.Handlers;

public class ProductChangedEventHandler(IProductSyncManager manager) : IHandleMessages<ProductChangedEvent>
{
    public Task Handle(ProductChangedEvent message) => manager.HandleAsync(message);
}
