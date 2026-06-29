using OpenHub.ProductManager.Api.Models.Entities;

namespace OpenHub.ProductManager.Api.Accessors.Interfaces;

public interface IProductPriceAccessor
{
    Task<IReadOnlyList<ProductPriceEntity>> GetActiveByProductIdAsync(Guid productId, CancellationToken ct = default);
    Task<ProductPriceEntity> SaveAsync(ProductPriceEntity entity, CancellationToken ct = default);
}
