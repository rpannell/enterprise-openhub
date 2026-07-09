using OpenHub.SalesOrg.Api.Managers.Models;
using OpenHub.SalesOrg.Api.Models.Requests;

namespace OpenHub.SalesOrg.Api.Managers.Interfaces;

public interface ISalesRoleManager
{
    Task<IReadOnlyList<SalesRoleDto>> GetAllAsync(CancellationToken ct = default);
    Task<SalesRoleDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SalesRoleDto> CreateAsync(CreateSalesRoleRequest request, CancellationToken ct = default);
    Task<SalesRoleDto?> UpdateAsync(Guid id, UpdateSalesRoleRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, CancellationToken ct = default);
}
