using OpenHub.SalesOrg.Api.Managers.Models;
using OpenHub.SalesOrg.Api.Models.Requests;

namespace OpenHub.SalesOrg.Api.Managers.Interfaces;

public interface ISalesOrganizationUnitManager
{
    Task<IReadOnlyList<SalesOrganizationUnitDto>> GetAllAsync(CancellationToken ct = default);
    Task<SalesOrganizationUnitDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SalesOrganizationUnitDto> CreateAsync(CreateSalesOrganizationUnitRequest request, Guid createUserId, CancellationToken ct = default);
    Task<SalesOrganizationUnitDto?> UpdateAsync(Guid id, UpdateSalesOrganizationUnitRequest request, Guid modifyUserId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid deleteUserId, CancellationToken ct = default);

    Task<IReadOnlyList<Guid>> GetDescendantsAsync(Guid unitId, CancellationToken ct = default);
    Task<IReadOnlyList<Guid>> GetAncestorsAsync(Guid unitId, CancellationToken ct = default);
}
