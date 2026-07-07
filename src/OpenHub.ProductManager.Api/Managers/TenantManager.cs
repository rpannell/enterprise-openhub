using OpenHub.ProductManager.Api.Accessors.Interfaces;
using OpenHub.ProductManager.Api.Managers.Interfaces;
using OpenHub.ProductManager.Api.Managers.Models;

namespace OpenHub.ProductManager.Api.Managers;

public class TenantManager(ITenantAccessor accessor) : ITenantManager
{
    public Task<IReadOnlyList<TenantDto>> ListActiveTenantsAsync(CancellationToken ct = default)
        => accessor.ListActiveAsync(ct);

    public Task SyncTenantAsync(TenantSyncDto dto, CancellationToken ct = default)
        => accessor.SyncTenantAsync(dto.TenantId, dto.RealmSlug, dto.Name, dto.IsDeleted, ct);

    public Task SyncUserAsync(UserSyncDto dto, CancellationToken ct = default)
        => accessor.SyncUserAsync(dto.TenantId, dto.UniqueId, dto.Email, dto.FirstName, dto.LastName, dto.IsDeleted, ct);
}
