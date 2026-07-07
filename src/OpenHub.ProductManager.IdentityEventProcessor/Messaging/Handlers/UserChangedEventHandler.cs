using Rebus.Handlers;
using OpenHub.Platform.IdentityEvents.Contracts;
using OpenHub.ProductManager.IdentityEventProcessor.Managers.Interfaces;

namespace OpenHub.ProductManager.IdentityEventProcessor.Messaging.Handlers;

public class UserChangedEventHandler(IUserSyncManager manager) : IHandleMessages<UserChangedEvent>
{
    public Task Handle(UserChangedEvent message) => manager.HandleAsync(message);
}
