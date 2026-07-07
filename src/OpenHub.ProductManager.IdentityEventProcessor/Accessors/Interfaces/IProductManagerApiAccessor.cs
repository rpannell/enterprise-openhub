namespace OpenHub.ProductManager.IdentityEventProcessor.Accessors.Interfaces;

public interface IProductManagerApiAccessor
{
    /// <summary>Upserts, or soft-deletes, the tenant row for <paramref name="tenantId"/>.</summary>
    Task SyncTenantAsync(
        Guid tenantId, string realmSlug, string? name, bool isDeleted, string accessToken,
        CancellationToken ct = default);

    /// <summary>Upserts, or soft-deletes, the user row for <paramref name="uniqueId"/> within <paramref name="tenantId"/>.</summary>
    Task SyncUserAsync(
        Guid tenantId, string uniqueId, string? email, string? firstName, string? lastName, bool isDeleted,
        string accessToken, CancellationToken ct = default);
}
