using OpenHub.ProductManager.Function.Accessors.Models;

namespace OpenHub.ProductManager.Function.Accessors.Interfaces;

public interface IProductManagerApiAccessor
{
    /// <summary>Fetches a product by ID, authenticated as the tenant behind <paramref name="accessToken"/>.</summary>
    Task<ProductApiResponse?> GetProductByIdAsync(Guid productId, string accessToken, CancellationToken ct = default);
}
