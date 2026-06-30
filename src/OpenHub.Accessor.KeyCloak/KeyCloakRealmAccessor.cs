using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.Accessor.KeyCloak.Models;

namespace OpenHub.Accessor.KeyCloak;

public class KeyCloakRealmAccessor(HttpClient http, IOptions<KeyCloakOptions> options)
    : IKeyCloakRealmAccessor
{
    private readonly KeyCloakOptions _opts = options.Value;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _adminToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    public async Task<KeyCloakRealmInfo?> GetRealmBySlugAsync(string slug, CancellationToken ct = default)
    {
        await EnsureAdminTokenAsync(ct);

        // Relative path — BaseAddress (set in AddKeyCloakAccessor) + service discovery handles the host
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/admin/realms/{Uri.EscapeDataString(slug)}");
        request.Headers.Authorization = new("Bearer", _adminToken);

        using var response = await http.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadFromJsonAsync<KeyCloakRealmResponse>(ct);
        if (json is null) return null;

        return new KeyCloakRealmInfo
        {
            Id = json.Id,
            Realm = json.Realm,
            DisplayName = string.IsNullOrWhiteSpace(json.DisplayName) ? null : json.DisplayName,
            Enabled = json.Enabled
        };
    }

    private async Task EnsureAdminTokenAsync(CancellationToken ct)
    {
        if (_adminToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
            return;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_adminToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
                return;

            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = "admin-cli",
                ["grant_type"] = "password",
                ["username"] = _opts.AdminUsername,
                ["password"] = _opts.AdminPassword
            });

            // Relative path — BaseAddress handles the Keycloak host
            using var response = await http.PostAsync(
                "/realms/master/protocol/openid-connect/token", body, ct);
            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<KeyCloakTokenResponse>(ct)
                ?? throw new InvalidOperationException("Failed to deserialize Keycloak admin token response.");

            _adminToken = token.AccessToken;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn - 30);
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private record KeyCloakTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);

    private record KeyCloakRealmResponse(
        [property: JsonPropertyName("id")]           string Id,
        [property: JsonPropertyName("realm")]        string Realm,
        [property: JsonPropertyName("displayName")]  string? DisplayName,
        [property: JsonPropertyName("enabled")]      bool Enabled);
}
