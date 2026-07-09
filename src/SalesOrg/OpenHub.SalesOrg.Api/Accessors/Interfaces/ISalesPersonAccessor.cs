using OpenHub.SalesOrg.Api.Accessors.Models;

namespace OpenHub.SalesOrg.Api.Accessors.Interfaces;

public interface ISalesPersonAccessor
{
    Task<SalesPersonEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<SalesPersonEntity>> GetAllAsync(CancellationToken ct = default);
    Task<SalesPersonEntity> CreateAsync(SalesPersonEntity entity, CancellationToken ct = default);
    Task<SalesPersonEntity> UpdateAsync(SalesPersonEntity entity, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);
}
