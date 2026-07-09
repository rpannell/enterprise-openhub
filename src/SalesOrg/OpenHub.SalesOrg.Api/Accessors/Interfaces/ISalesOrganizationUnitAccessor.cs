using OpenHub.SalesOrg.Api.Accessors.Models;

namespace OpenHub.SalesOrg.Api.Accessors.Interfaces;

public interface ISalesOrganizationUnitAccessor
{
    Task<SalesOrganizationUnitEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SalesOrganizationUnitEntity>> GetAllAsync(CancellationToken ct = default);
    Task<SalesOrganizationUnitEntity> CreateAsync(SalesOrganizationUnitEntity entity, CancellationToken ct = default);
    Task<SalesOrganizationUnitEntity> UpdateAsync(SalesOrganizationUnitEntity entity, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, Guid deleteUserId, CancellationToken ct = default);
}
