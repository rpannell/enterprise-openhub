using System.Net.Http.Json;
using System.Text.Json.Serialization;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.Accessor.KeyCloak.Internal;
using OpenHub.Accessor.KeyCloak.Models;

namespace OpenHub.Accessor.KeyCloak;

public class KeyCloakRealmAccessor(HttpClient http, KeyCloakAdminTokenProvider tokenProvider)
    : IKeyCloakRealmAccessor
{
    public async Task<KeyCloakRealmInfo?> GetRealmBySlugAsync(string slug, CancellationToken ct = default)
    {
        var adminToken = await tokenProvider.GetAdminTokenAsync(http, ct);

        // Relative path — BaseAddress (set in AddKeyCloakAccessor) + service discovery handles the host
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/admin/realms/{Uri.EscapeDataString(slug)}");
        request.Headers.Authorization = new("Bearer", adminToken);

        using var response = await http.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<KeyCloakRealmResponse>(ct);
        if (json is null) return null;

        return ToInfo(json);
    }

    public async Task<IReadOnlyList<KeyCloakRealmInfo>> ListRealmsAsync(CancellationToken ct = default)
    {
        var adminToken = await tokenProvider.GetAdminTokenAsync(http, ct);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/admin/realms");
        request.Headers.Authorization = new("Bearer", adminToken);

        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<List<KeyCloakRealmResponse>>(ct) ?? [];
        return json.Select(ToInfo).ToList();
    }

    private static KeyCloakRealmInfo ToInfo(KeyCloakRealmResponse json) => new()
    {
        Id = json.Id,
        Realm = json.Realm,
        DisplayName = string.IsNullOrWhiteSpace(json.DisplayName) ? null : json.DisplayName,
        Enabled = json.Enabled
    };

    private record KeyCloakRealmResponse(
        [property: JsonPropertyName("id")]           string Id,
        [property: JsonPropertyName("realm")]        string Realm,
        [property: JsonPropertyName("displayName")]  string? DisplayName,
        [property: JsonPropertyName("enabled")]      bool Enabled);
}
