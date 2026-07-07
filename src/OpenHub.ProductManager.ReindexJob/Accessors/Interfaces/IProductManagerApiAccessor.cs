using OpenHub.ProductManager.ReindexJob.Accessors.Models;

namespace OpenHub.ProductManager.ReindexJob.Accessors.Interfaces;

public interface IProductManagerApiAccessor
{
    /// <summary>Lists every active tenant, authenticated as the platform-admin M2M client.</summary>
    Task<IReadOnlyList<TenantSummaryApiResponse>> GetTenantsAsync(string adminAccessToken, CancellationToken ct = default);

    /// <summary>Fetches one page of products, authenticated as the tenant behind <paramref name="accessToken"/>.</summary>
    Task<PagedApiResponse<ProductApiResponse>> GetProductsPageAsync(
        string accessToken, int page, int pageSize, CancellationToken ct = default);
}
