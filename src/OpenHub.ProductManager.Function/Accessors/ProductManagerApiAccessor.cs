using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OpenHub.ProductManager.Function.Accessors.Interfaces;
using OpenHub.ProductManager.Function.Accessors.Models;

namespace OpenHub.ProductManager.Function.Accessors;

public class ProductManagerApiAccessor(HttpClient http) : IProductManagerApiAccessor
{
    public async Task<ProductApiResponse?> GetProductByIdAsync(Guid productId, string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/products/{productId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ProductApiResponse>(ct);
    }
}
