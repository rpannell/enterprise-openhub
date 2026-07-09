using OpenHub.SalesOrg.Api.Managers.Models;
using OpenHub.SalesOrg.Api.Models.Requests;

namespace OpenHub.SalesOrg.Api.Managers.Interfaces;

public interface ISalesAssignmentManager
{
    Task<IReadOnlyList<SalesAssignmentDto>> GetAllAsync(CancellationToken ct = default);
    Task<SalesAssignmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SalesAssignmentDto> CreateAsync(CreateSalesAssignmentRequest request, Guid createUserId, CancellationToken ct = default);
    Task<SalesAssignmentDto?> UpdateAsync(Guid id, UpdateSalesAssignmentRequest request, Guid modifyUserId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid deleteUserId, CancellationToken ct = default);
}
