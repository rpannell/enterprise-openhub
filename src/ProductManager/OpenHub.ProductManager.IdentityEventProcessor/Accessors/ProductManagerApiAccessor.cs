using System.Net.Http.Headers;
using System.Net.Http.Json;
using OpenHub.ProductManager.IdentityEventProcessor.Accessors.Interfaces;
using OpenHub.ProductManager.IdentityEventProcessor.Accessors.Models;

namespace OpenHub.ProductManager.IdentityEventProcessor.Accessors;

public class ProductManagerApiAccessor(HttpClient http) : IProductManagerApiAccessor
{
    public async Task SyncTenantAsync(
        Guid tenantId, string realmSlug, string? name, bool isDeleted, string accessToken,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/tenants/sync")
        {
            Content = JsonContent.Create(new SyncTenantApiRequest
            {
                TenantId = tenantId,
                RealmSlug = realmSlug,
                Name = name,
                IsDeleted = isDeleted
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task SyncUserAsync(
        Guid tenantId, string uniqueId, string? email, string? firstName, string? lastName, bool isDeleted,
        string accessToken, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/users/sync")
        {
            Content = JsonContent.Create(new SyncUserApiRequest
            {
                TenantId = tenantId,
                UniqueId = uniqueId,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                IsDeleted = isDeleted
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}
