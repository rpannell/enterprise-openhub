using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using OpenHub.CRM.Api.Accessors.Interfaces;
using OpenHub.CRM.Api.Accessors.Models;

namespace OpenHub.CRM.Api.Accessors;

public class SalesOrgApiAccessor(HttpClient http, IHttpContextAccessor httpContextAccessor) : ISalesOrgApiAccessor
{
    // Forwards the interactive caller's own bearer token rather than using a service-account
    // flow — CRM.Api only ever calls SalesOrg.Api from inside an already-authenticated request,
    // so the calling user's own token is available and already carries the right tenant/realm
    // claims. Revisit for a service-account flow only if SalesOrg later needs to expose data the
    // calling user shouldn't see directly.
    private void ApplyCallerBearerToken(HttpRequestMessage request)
    {
        var authorizationHeader = httpContextAccessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrEmpty(authorizationHeader))
            request.Headers.TryAddWithoutValidation("Authorization", authorizationHeader);
    }

    public async Task<SalesPersonApiResponse?> GetSalesPersonByIdAsync(Guid salesPersonId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/sales-people/{salesPersonId}");
        ApplyCallerBearerToken(request);

        using var response = await http.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<SalesPersonApiResponse>(ct);
    }

    public async Task<IReadOnlyList<Guid>> GetReportSalesPersonIdsAsync(Guid salesPersonId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/sales-people/{salesPersonId}/reports");
        ApplyCallerBearerToken(request);

        using var response = await http.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();

        var reports = await response.Content.ReadFromJsonAsync<List<SalesPersonApiResponse>>(ct) ?? [];
        return [.. reports.Select(r => r.Id)];
    }
}
