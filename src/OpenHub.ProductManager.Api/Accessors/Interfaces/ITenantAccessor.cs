using OpenHub.ProductManager.Api.Managers.Models;

namespace OpenHub.ProductManager.Api.Accessors.Interfaces;

public interface ITenantAccessor
{
    Task<TenantDto?> GetOrProvisionAsync(Guid tenantId, string realmSlug, CancellationToken ct = default);
}
