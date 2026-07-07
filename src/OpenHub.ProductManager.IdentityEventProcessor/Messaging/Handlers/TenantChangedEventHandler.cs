using Rebus.Handlers;
using OpenHub.Platform.IdentityEvents.Contracts;
using OpenHub.ProductManager.IdentityEventProcessor.Managers.Interfaces;

namespace OpenHub.ProductManager.IdentityEventProcessor.Messaging.Handlers;

public class TenantChangedEventHandler(ITenantSyncManager manager) : IHandleMessages<TenantChangedEvent>
{
    public Task Handle(TenantChangedEvent message) => manager.HandleAsync(message);
}
