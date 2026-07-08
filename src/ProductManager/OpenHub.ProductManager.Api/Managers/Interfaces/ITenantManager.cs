using OpenHub.ProductManager.Api.Managers.Models;

namespace OpenHub.ProductManager.Api.Managers.Interfaces;

public interface ITenantManager
{
    /// <summary>Lists every active tenant — platform-admin only, not tenant-scoped.</summary>
    Task<IReadOnlyList<TenantDto>> ListActiveTenantsAsync(CancellationToken ct = default);

    /// <summary>
    /// Upserts or soft-deletes a tenant driven by a Keycloak organization change — platform-admin
    /// only. Backs the sync endpoint OpenHub.ProductManager.IdentityEventProcessor calls.
    /// </summary>
    Task SyncTenantAsync(TenantSyncDto dto, CancellationToken ct = default);

    /// <summary>
    /// Upserts or soft-deletes a user driven by a Keycloak user/organization-membership change —
    /// platform-admin only. Backs the sync endpoint OpenHub.ProductManager.IdentityEventProcessor
    /// calls.
    /// </summary>
    Task SyncUserAsync(UserSyncDto dto, CancellationToken ct = default);
}
