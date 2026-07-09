using OpenHub.SalesOrg.Api.Accessors.Models;

namespace OpenHub.SalesOrg.Api.Accessors.Interfaces;

public interface ISalesAssignmentAccessor
{
    Task<SalesAssignmentEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SalesAssignmentEntity>> GetAllAsync(CancellationToken ct = default);
    Task<SalesAssignmentEntity> CreateAsync(SalesAssignmentEntity entity, CancellationToken ct = default);
    Task<SalesAssignmentEntity> UpdateAsync(SalesAssignmentEntity entity, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, Guid deleteUserId, CancellationToken ct = default);
}
