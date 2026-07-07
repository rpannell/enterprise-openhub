using Microsoft.Extensions.Logging;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.Platform.IdentityEvents.Contracts;
using OpenHub.ProductManager.IdentityEventProcessor.Accessors.Interfaces;
using OpenHub.ProductManager.IdentityEventProcessor.Managers.Interfaces;

namespace OpenHub.ProductManager.IdentityEventProcessor.Managers;

public class UserSyncManager(
    IKeyCloakM2MTokenAccessor m2mAccessor,
    IProductManagerApiAccessor apiAccessor,
    ILogger<UserSyncManager> logger) : IUserSyncManager
{
    private const string PlatformAdminRealmSlug = "platform-admin";

    public async Task HandleAsync(UserChangedEvent message, CancellationToken ct = default)
    {
        if (message.TenantId is null)
        {
            // catalog.users is tenant-scoped (RLS) — a plain USER admin event with no
            // organization-membership context can't be synced there. Nothing to do until the
            // user is associated with a tenant via an ORGANIZATION_MEMBERSHIP event.
            logger.LogDebug(
                "Skipping user {UserId} sync — no TenantId on the event (not an organization-membership change).",
                message.UserId);
            return;
        }

        var isDeleted = message.Operation == IdentityChangeOperation.Deleted;

        var adminToken = await m2mAccessor.GetTokenAsync(PlatformAdminRealmSlug, ct);
        await apiAccessor.SyncUserAsync(
            message.TenantId.Value, message.UserId, message.Email, message.FirstName, message.LastName,
            isDeleted, adminToken, ct);

        logger.LogInformation(
            "Synced user {UserId} in tenant {TenantId}, operation={Operation}.",
            message.UserId, message.TenantId, message.Operation);
    }
}
