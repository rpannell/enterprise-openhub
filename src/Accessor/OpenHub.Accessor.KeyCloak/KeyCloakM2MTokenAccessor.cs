using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.Accessor.KeyCloak.Models;

namespace OpenHub.Accessor.KeyCloak;

public class KeyCloakM2MTokenAccessor(HttpClient http, IOptions<M2MOptions> options, HybridCache cache)
    : IKeyCloakM2MTokenAccessor
{
    private readonly M2MOptions _opts = options.Value;

    private static readonly HybridCacheEntryOptions TokenCacheOptions = new()
    {
        // Conservative fixed TTL rather than trying to thread the token
        // response's expires_in through HybridCache's static-options API.
        // Keycloak client-credentials tokens default to 5+ minutes.
        Expiration = TimeSpan.FromMinutes(4),
        LocalCacheExpiration = TimeSpan.FromMinutes(4),
    };

    public async Task<string> GetTokenAsync(string realmSlug, CancellationToken ct = default)
        => await cache.GetOrCreateAsync(
            $"m2m-token:{realmSlug}",
            async innerCt => await FetchTokenAsync(realmSlug, innerCt),
            TokenCacheOptions,
            cancellationToken: ct);

    private async Task<string> FetchTokenAsync(string realmSlug, CancellationToken ct)
    {
        if (!_opts.ClientSecrets.TryGetValue(realmSlug, out var clientSecret))
            throw new InvalidOperationException(
                $"No M2M client secret configured for realm '{realmSlug}'. Set KeyCloak:M2M:ClientSecrets:{realmSlug}.");

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _opts.ClientId,
            ["grant_type"] = "client_credentials",
            ["client_secret"] = clientSecret,
        });

        using var response = await http.PostAsync(
            $"/realms/{Uri.EscapeDataString(realmSlug)}/protocol/openid-connect/token", body, ct);
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<M2MTokenResponse>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize Keycloak M2M token response.");

        return token.AccessToken;
    }

    private record M2MTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken);
}
