using OpenHub.SalesOrg.Api.Accessors.Models;

namespace OpenHub.SalesOrg.Api.Accessors.Interfaces;

public interface ISalesRoleAccessor
{
    Task<SalesRoleEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SalesRoleEntity>> GetAllAsync(CancellationToken ct = default);
    Task<SalesRoleEntity> CreateAsync(SalesRoleEntity entity, CancellationToken ct = default);
    Task<SalesRoleEntity> UpdateAsync(SalesRoleEntity entity, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
}
