using OpenHub.ProductManager.Api.Models.Entities;

namespace OpenHub.ProductManager.Api.Accessors.Interfaces;

public interface IProductAccessor
{
    Task<ProductEntity?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ProductEntity>> GetAllAsync(CancellationToken ct = default);
    Task<ProductEntity> CreateAsync(ProductEntity entity, CancellationToken ct = default);
    Task<ProductEntity> UpdateAsync(ProductEntity entity, CancellationToken ct = default);
    Task SoftDeleteAsync(Guid id, Guid deleteUserId, CancellationToken ct = default);
}
