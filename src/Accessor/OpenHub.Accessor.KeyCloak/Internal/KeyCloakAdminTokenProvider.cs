using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace OpenHub.Accessor.KeyCloak.Internal;

/// <summary>
/// Fetches and caches the admin-cli password-grant token used by every Keycloak Admin
/// REST accessor (realm, organization, user). Shared as a singleton so the cached token
/// survives across accessor instances instead of each typed-client resolution
/// re-authenticating — the HttpClient used for the token call is supplied by the caller
/// (each accessor's own typed client, already pointed at the Keycloak base address).
/// </summary>
public sealed class KeyCloakAdminTokenProvider(IOptions<KeyCloakOptions> options)
{
    private readonly KeyCloakOptions _opts = options.Value;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _adminToken;
    private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;

    public async Task<string> GetAdminTokenAsync(HttpClient http, CancellationToken ct = default)
    {
        if (_adminToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
            return _adminToken;

        await _tokenLock.WaitAsync(ct);
        try
        {
            if (_adminToken is not null && DateTimeOffset.UtcNow < _tokenExpiresAt)
                return _adminToken;

            var body = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = "admin-cli",
                ["grant_type"] = "password",
                ["username"] = _opts.AdminUsername,
                ["password"] = _opts.AdminPassword
            });

            using var response = await http.PostAsync(
                "/realms/master/protocol/openid-connect/token", body, ct);
            response.EnsureSuccessStatusCode();

            var token = await response.Content.ReadFromJsonAsync<KeyCloakTokenResponse>(ct)
                ?? throw new InvalidOperationException("Failed to deserialize Keycloak admin token response.");

            _adminToken = token.AccessToken;
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn - 30);
            return _adminToken;
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private record KeyCloakTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn);
}
