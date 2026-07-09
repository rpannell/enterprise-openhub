using OpenHub.SalesOrg.Api.Managers.Models;

namespace OpenHub.SalesOrg.Api.Accessors.Interfaces;

public interface ITenantAccessor
{
    Task<TenantDto?> GetOrProvisionAsync(Guid tenantId, string realmSlug, CancellationToken ct = default);

    Task<Guid?> GetOrProvisionUserAsync(
        Guid tenantId, string uniqueId, string email, string firstName, string lastName,
        CancellationToken ct = default);
}
