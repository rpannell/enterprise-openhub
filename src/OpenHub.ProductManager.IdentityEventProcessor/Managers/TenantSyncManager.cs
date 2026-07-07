using Microsoft.Extensions.Logging;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.Platform.IdentityEvents.Contracts;
using OpenHub.ProductManager.IdentityEventProcessor.Accessors.Interfaces;
using OpenHub.ProductManager.IdentityEventProcessor.Managers.Interfaces;

namespace OpenHub.ProductManager.IdentityEventProcessor.Managers;

public class TenantSyncManager(
    IKeyCloakM2MTokenAccessor m2mAccessor,
    IKeyCloakRealmAccessor realmAccessor,
    IProductManagerApiAccessor apiAccessor,
    ILogger<TenantSyncManager> logger) : ITenantSyncManager
{
    // Must match the realm slug used when provisioning the platform-admin Keycloak client
    // and its KeyCloak:M2M:ClientSecrets entry — same constant/pattern as
    // OpenHub.ProductManager.ReindexJob's ReindexManager.
    private const string PlatformAdminRealmSlug = "platform-admin";

    public async Task HandleAsync(TenantChangedEvent message, CancellationToken ct = default)
    {
        var isDeleted = message.Operation == IdentityChangeOperation.Deleted;

        // On create/update, prefer the canonical display name from Keycloak's admin API over
        // whatever the event carried, if it's available — the event's Name is a point-in-time
        // snapshot from the admin action's representation and may be stale or absent.
        var name = message.Name;
        if (!isDeleted)
        {
            var realmInfo = await realmAccessor.GetRealmBySlugAsync(message.RealmSlug, ct);
            if (realmInfo?.DisplayName is { Length: > 0 })
                name = realmInfo.DisplayName;
        }

        var adminToken = await m2mAccessor.GetTokenAsync(PlatformAdminRealmSlug, ct);
        await apiAccessor.SyncTenantAsync(message.TenantId, message.RealmSlug, name, isDeleted, adminToken, ct);

        logger.LogInformation(
            "Synced tenant {TenantId} ({RealmSlug}), operation={Operation}.",
            message.TenantId, message.RealmSlug, message.Operation);
    }
}
