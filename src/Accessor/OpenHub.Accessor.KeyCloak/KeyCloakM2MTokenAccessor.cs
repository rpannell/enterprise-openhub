using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.Accessor.KeyCloak.Models;

namespace OpenHub.Accessor.KeyCloak;

public class KeyCloakM2MTokenAccessor(HttpClient http, IOptions<M2MOptions> options, HybridCache cache, ILogger<KeyCloakM2MTokenAccessor> logger)
    : IKeyCloakM2MTokenAccessor
{
    private readonly M2MOptions _opts = options.Value;
    private readonly ILogger<KeyCloakM2MTokenAccessor> _logger = logger;

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
            CacheKey(realmSlug),
            async innerCt => await FetchTokenAsync(realmSlug, innerCt),
            TokenCacheOptions,
            cancellationToken: ct);

    public async Task InvalidateTokenAsync(string realmSlug, CancellationToken ct = default)
        => await cache.RemoveAsync(CacheKey(realmSlug), ct);

    private static string CacheKey(string realmSlug) => $"m2m-token:{realmSlug}";

    private async Task<string> FetchTokenAsync(string realmSlug, CancellationToken ct)
    {
        if (!_opts.ClientSecrets.TryGetValue(realmSlug, out var clientSecret))
        {
            _logger.LogError(
                "No M2M client secret configured for realm '{RealmSlug}'. Available secrets: {Secrets}",
                realmSlug,
                string.Join(", ", _opts.ClientSecrets.Keys));
            throw new InvalidOperationException(
                $"No M2M client secret configured for realm '{realmSlug}'. Set KeyCloak:M2M:ClientSecrets:{realmSlug}.");
        }

        _logger.LogInformation(
            "Fetching M2M token for realm '{RealmSlug}' with clientId '{ClientId}', clientSecret length: {SecretLength}",
            realmSlug,
            _opts.ClientId,
            clientSecret?.Length ?? 0);

        var body = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _opts.ClientId,
            ["grant_type"] = "client_credentials",
            ["client_secret"] = clientSecret ?? string.Empty,
        });

        var tokenUrl = $"/realms/{Uri.EscapeDataString(realmSlug)}/protocol/openid-connect/token";
        _logger.LogDebug("Token URL: {TokenUrl}", tokenUrl);
        
        using var response = await http.PostAsync(tokenUrl, body, ct);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError(
                "Keycloak returned {StatusCode} for realm '{RealmSlug}': {Error}",
                response.StatusCode,
                realmSlug,
                errorContent);
        }
        
        response.EnsureSuccessStatusCode();

        var token = await response.Content.ReadFromJsonAsync<M2MTokenResponse>(ct)
            ?? throw new InvalidOperationException("Failed to deserialize Keycloak M2M token response.");

        _logger.LogInformation("Successfully fetched M2M token for realm '{RealmSlug}'", realmSlug);
        return token.AccessToken;
    }

    private record M2MTokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken);
}
