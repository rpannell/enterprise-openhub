using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.Platform.IdentityEvents.Contracts;
using OpenHub.ProductManager.IdentityEventProcessor.Managers.Interfaces;
using OpenHub.ProductManager.IdentityEventProcessor.Messaging.Handlers;

namespace OpenHub.ProductManager.IdentityEventProcessor.Tests.Unit;

public class UserChangedEventHandlerTests
{
    [Fact]
    public async Task Handle_DelegatesToUserSyncManager_WithSameEvent()
    {
        var manager = Substitute.For<IUserSyncManager>();
        var handler = new UserChangedEventHandler(manager);
        var evt = new UserChangedEvent
        {
            UserId = Guid.NewGuid().ToString(),
            TenantId = Guid.NewGuid(),
            RealmSlug = "client-a",
            Operation = IdentityChangeOperation.Created,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Email = "a@example.com",
        };

        await handler.Handle(evt);

        await manager.Received(1).HandleAsync(evt, Arg.Any<CancellationToken>());
    }
}
