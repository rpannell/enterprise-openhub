using System.Net.Http.Json;
using System.Text.Json.Serialization;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.Accessor.KeyCloak.Internal;
using OpenHub.Accessor.KeyCloak.Models;

namespace OpenHub.Accessor.KeyCloak;

public class KeyCloakOrganizationAccessor(HttpClient http, KeyCloakAdminTokenProvider tokenProvider)
    : IKeyCloakOrganizationAccessor
{
    // Keycloak's admin API defaults "max" to 100 when the query param is omitted, silently
    // truncating the result for any realm with more organizations than that. See the identical
    // note on KeyCloakUserAccessor.ListUsersAsync — same gotcha, same fix.
    private const int MaxOrganizationsPerListCall = 1000;

    public async Task<string> CreateOrganizationAsync(
        string realmSlug, CreateKeyCloakOrganizationRequest request, CancellationToken ct = default)
    {
        var adminToken = await tokenProvider.GetAdminTokenAsync(http, ct);

        var payload = new OrganizationPayload(
            request.Name,
            request.Alias,
            request.Enabled,
            request.Domains.Select(d => new DomainPayload(d, false)).ToArray());

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/admin/realms/{Uri.EscapeDataString(realmSlug)}/organizations")
        {
            Content = JsonContent.Create(payload)
        };
        httpRequest.Headers.Authorization = new("Bearer", adminToken);

        using var response = await http.SendAsync(httpRequest, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            throw new KeyCloakAdminConflictException(
                $"An organization with alias '{request.Alias}' already exists in realm '{realmSlug}'.");

        response.EnsureSuccessStatusCode();

        var location = response.Headers.Location
            ?? throw new InvalidOperationException("Keycloak did not return a Location header for the created organization.");
        return location.Segments[^1];
    }

    public async Task<KeyCloakOrganizationInfo?> GetOrganizationAsync(
        string realmSlug, string organizationId, CancellationToken ct = default)
    {
        var adminToken = await tokenProvider.GetAdminTokenAsync(http, ct);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/admin/realms/{Uri.EscapeDataString(realmSlug)}/organizations/{Uri.EscapeDataString(organizationId)}");
        request.Headers.Authorization = new("Bearer", adminToken);

        using var response = await http.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<OrganizationResponse>(ct);
        return json is null ? null : ToInfo(json);
    }

    public async Task<IReadOnlyList<KeyCloakOrganizationInfo>> ListOrganizationsAsync(
        string realmSlug, CancellationToken ct = default)
    {
        var adminToken = await tokenProvider.GetAdminTokenAsync(http, ct);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/admin/realms/{Uri.EscapeDataString(realmSlug)}/organizations?max={MaxOrganizationsPerListCall}");
        request.Headers.Authorization = new("Bearer", adminToken);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<List<OrganizationResponse>>(ct) ?? [];
        return json.Select(ToInfo).ToList();
    }

    public async Task AddMemberAsync(
        string realmSlug, string organizationId, string userId, CancellationToken ct = default)
    {
        var adminToken = await tokenProvider.GetAdminTokenAsync(http, ct);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/admin/realms/{Uri.EscapeDataString(realmSlug)}/organizations/{Uri.EscapeDataString(organizationId)}/members")
        {
            // Keycloak's org-member endpoint expects the raw user id as a bare JSON string body.
            Content = JsonContent.Create(userId)
        };
        request.Headers.Authorization = new("Bearer", adminToken);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<IReadOnlyList<KeyCloakUserInfo>> ListMembersAsync(
        string realmSlug, string organizationId, CancellationToken ct = default)
    {
        var adminToken = await tokenProvider.GetAdminTokenAsync(http, ct);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/admin/realms/{Uri.EscapeDataString(realmSlug)}/organizations/{Uri.EscapeDataString(organizationId)}/members");
        request.Headers.Authorization = new("Bearer", adminToken);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<List<KeyCloakUserRepresentation>>(ct) ?? [];
        return json.Select(u => u.ToInfo()).ToList();
    }

    private static KeyCloakOrganizationInfo ToInfo(OrganizationResponse json) => new()
    {
        Id = json.Id,
        Name = json.Name,
        Alias = json.Alias,
        Enabled = json.Enabled,
        Domains = json.Domains?.Select(d => d.Name).ToList() ?? []
    };

    private record OrganizationPayload(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("alias")] string Alias,
        [property: JsonPropertyName("enabled")] bool Enabled,
        [property: JsonPropertyName("domains")] DomainPayload[] Domains);

    private record DomainPayload(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("verified")] bool Verified);

    private record OrganizationResponse(
        [property: JsonPropertyName("id")] string Id,
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("alias")] string Alias,
        [property: JsonPropertyName("enabled")] bool Enabled,
        [property: JsonPropertyName("domains")] List<DomainResponse>? Domains);

    private record DomainResponse([property: JsonPropertyName("name")] string Name);
}
