using System.Net.Http.Headers;
using System.Net.Http.Json;
using OpenHub.ProductManager.ReindexJob.Accessors.Interfaces;
using OpenHub.ProductManager.ReindexJob.Accessors.Models;

namespace OpenHub.ProductManager.ReindexJob.Accessors;

public class ProductManagerApiAccessor(HttpClient http) : IProductManagerApiAccessor
{
    public async Task<IReadOnlyList<TenantSummaryApiResponse>> GetTenantsAsync(string adminAccessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/admin/tenants");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", adminAccessToken);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<List<TenantSummaryApiResponse>>(ct) ?? [];
    }

    public async Task<PagedApiResponse<ProductApiResponse>> GetProductsPageAsync(
        string accessToken, int page, int pageSize, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/products?page={page}&pageSize={pageSize}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PagedApiResponse<ProductApiResponse>>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize a products page response.");
    }
}
