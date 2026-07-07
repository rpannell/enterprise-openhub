using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.Platform.IdentityEvents.Contracts;
using OpenHub.ProductManager.IdentityEventProcessor.Accessors.Interfaces;
using OpenHub.ProductManager.IdentityEventProcessor.Managers;

namespace OpenHub.ProductManager.IdentityEventProcessor.Tests.Unit;

public class UserSyncManagerTests
{
    [Fact]
    public async Task HandleAsync_NoTenantId_SkipsSync()
    {
        var m2m = Substitute.For<IKeyCloakM2MTokenAccessor>();
        var apiAccessor = Substitute.For<IProductManagerApiAccessor>();
        var manager = new UserSyncManager(m2m, apiAccessor, NullLogger<UserSyncManager>.Instance);

        var evt = new UserChangedEvent
        {
            UserId = Guid.NewGuid().ToString(),
            TenantId = null,
            RealmSlug = "client-a",
            Operation = IdentityChangeOperation.Updated,
            OccurredAtUtc = DateTimeOffset.UtcNow,
        };

        await manager.HandleAsync(evt);

        await m2m.DidNotReceive().GetTokenAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await apiAccessor.DidNotReceive().SyncUserAsync(
            Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<bool>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithTenantId_SyncsUser()
    {
        var m2m = Substitute.For<IKeyCloakM2MTokenAccessor>();
        m2m.GetTokenAsync("platform-admin", Arg.Any<CancellationToken>()).Returns("admin-token");

        var apiAccessor = Substitute.For<IProductManagerApiAccessor>();
        var manager = new UserSyncManager(m2m, apiAccessor, NullLogger<UserSyncManager>.Instance);

        var evt = new UserChangedEvent
        {
            UserId = "kc-user-1",
            TenantId = Guid.NewGuid(),
            RealmSlug = "client-a",
            Operation = IdentityChangeOperation.Created,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Email = "a@example.com",
            FirstName = "Ada",
            LastName = "Lovelace",
        };

        await manager.HandleAsync(evt);

        await apiAccessor.Received(1).SyncUserAsync(
            evt.TenantId!.Value, evt.UserId, evt.Email, evt.FirstName, evt.LastName,
            isDeleted: false, "admin-token", Arg.Any<CancellationToken>());
    }
}
