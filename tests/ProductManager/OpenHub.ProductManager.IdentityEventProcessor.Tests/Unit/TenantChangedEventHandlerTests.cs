using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.Platform.IdentityEvents.Contracts;
using OpenHub.ProductManager.IdentityEventProcessor.Managers.Interfaces;
using OpenHub.ProductManager.IdentityEventProcessor.Messaging.Handlers;

namespace OpenHub.ProductManager.IdentityEventProcessor.Tests.Unit;

public class TenantChangedEventHandlerTests
{
    [Fact]
    public async Task Handle_DelegatesToTenantSyncManager_WithSameEvent()
    {
        var manager = Substitute.For<ITenantSyncManager>();
        var handler = new TenantChangedEventHandler(manager);
        var evt = new TenantChangedEvent
        {
            TenantId = Guid.NewGuid(),
            RealmSlug = "client-a",
            Operation = IdentityChangeOperation.Updated,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Name = "Acme Corp",
        };

        await handler.Handle(evt);

        await manager.Received(1).HandleAsync(evt, Arg.Any<CancellationToken>());
    }
}
