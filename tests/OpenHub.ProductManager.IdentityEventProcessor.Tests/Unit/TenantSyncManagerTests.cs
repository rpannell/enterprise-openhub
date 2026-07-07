using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.Accessor.KeyCloak.Models;
using OpenHub.Platform.IdentityEvents.Contracts;
using OpenHub.ProductManager.IdentityEventProcessor.Accessors.Interfaces;
using OpenHub.ProductManager.IdentityEventProcessor.Managers;

namespace OpenHub.ProductManager.IdentityEventProcessor.Tests.Unit;

public class TenantSyncManagerTests
{
    private static TenantChangedEvent MakeEvent(IdentityChangeOperation operation, string? name = "Event Name") =>
        new()
        {
            TenantId = Guid.NewGuid(),
            RealmSlug = "client-a",
            Operation = operation,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Name = name,
        };

    [Fact]
    public async Task HandleAsync_Upsert_PrefersCanonicalRealmDisplayNameOverEventName()
    {
        var m2m = Substitute.For<IKeyCloakM2MTokenAccessor>();
        m2m.GetTokenAsync("platform-admin", Arg.Any<CancellationToken>()).Returns("admin-token");

        var realmAccessor = Substitute.For<IKeyCloakRealmAccessor>();
        realmAccessor.GetRealmBySlugAsync("client-a", Arg.Any<CancellationToken>())
            .Returns(new KeyCloakRealmInfo { Id = "1", Realm = "client-a", DisplayName = "Canonical Name", Enabled = true });

        var apiAccessor = Substitute.For<IProductManagerApiAccessor>();
        var manager = new TenantSyncManager(m2m, realmAccessor, apiAccessor, NullLogger<TenantSyncManager>.Instance);

        var evt = MakeEvent(IdentityChangeOperation.Updated, name: "Stale Event Name");

        await manager.HandleAsync(evt);

        await apiAccessor.Received(1).SyncTenantAsync(
            evt.TenantId, evt.RealmSlug, "Canonical Name", isDeleted: false, "admin-token", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_Delete_DoesNotLookUpRealmInfo_AndPassesIsDeletedTrue()
    {
        var m2m = Substitute.For<IKeyCloakM2MTokenAccessor>();
        m2m.GetTokenAsync("platform-admin", Arg.Any<CancellationToken>()).Returns("admin-token");

        var realmAccessor = Substitute.For<IKeyCloakRealmAccessor>();
        var apiAccessor = Substitute.For<IProductManagerApiAccessor>();
        var manager = new TenantSyncManager(m2m, realmAccessor, apiAccessor, NullLogger<TenantSyncManager>.Instance);

        var evt = MakeEvent(IdentityChangeOperation.Deleted);

        await manager.HandleAsync(evt);

        await realmAccessor.DidNotReceive().GetRealmBySlugAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await apiAccessor.Received(1).SyncTenantAsync(
            evt.TenantId, evt.RealmSlug, evt.Name, isDeleted: true, "admin-token", Arg.Any<CancellationToken>());
    }
}
