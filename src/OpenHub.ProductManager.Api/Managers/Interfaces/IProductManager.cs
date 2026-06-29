using OpenHub.ProductManager.Api.Models.Dtos;
using OpenHub.ProductManager.Api.Models.Requests;

namespace OpenHub.ProductManager.Api.Managers.Interfaces;

public interface IProductManager
{
    Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken ct = default);
    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(CreateProductRequest request, Guid createUserId, CancellationToken ct = default);
    Task<ProductDto?> UpdateAsync(Guid id, UpdateProductRequest request, Guid modifyUserId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid id, Guid deleteUserId, CancellationToken ct = default);
}
