using OpenHub.ProductManager.Api.Managers.Models;

namespace OpenHub.ProductManager.Api.Accessors.Interfaces;

public interface ITenantAccessor
{
    Task<TenantDto?> GetOrProvisionAsync(Guid tenantId, string realmSlug, CancellationToken ct = default);

    Task<Guid?> GetOrProvisionUserAsync(
        Guid tenantId, string uniqueId, string email, string firstName, string lastName,
        CancellationToken ct = default);

    /// <summary>
    /// Lists every active tenant. Genuinely cross-tenant — catalog.tenants is intentionally
    /// excluded from Row Level Security (see V1_5__create_rls_policies.sql), so this needs no
    /// app.current_tenant_id session context, unlike every other accessor in this project.
    /// Backs the platform-admin "list tenants" endpoint, not any per-tenant caller.
    /// </summary>
    Task<IReadOnlyList<TenantDto>> ListActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Upserts (or soft-deletes, when <paramref name="isDeleted"/>) the tenant row driven by a
    /// Keycloak organization change, and refreshes the <c>tenant:id:{tenantId}</c> HybridCache
    /// entry so the next lookup reflects it. Backs the platform-admin tenant sync endpoint used
    /// by OpenHub.ProductManager.IdentityEventProcessor. A no-op if the tenant doesn't exist yet
    /// and this is a delete.
    /// </summary>
    Task SyncTenantAsync(Guid tenantId, string realmSlug, string? name, bool isDeleted, CancellationToken ct = default);

    /// <summary>
    /// Upserts (or soft-deletes) the user row driven by a Keycloak user/organization-membership
    /// change, and refreshes the <c>user:{tenantId}:{uniqueId}</c> HybridCache entry. Backs the
    /// platform-admin user sync endpoint used by OpenHub.ProductManager.IdentityEventProcessor.
    /// </summary>
    Task SyncUserAsync(
        Guid tenantId, string uniqueId, string? email, string? firstName, string? lastName,
        bool isDeleted, CancellationToken ct = default);
}
