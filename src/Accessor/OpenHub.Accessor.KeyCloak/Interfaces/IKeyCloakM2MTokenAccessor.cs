namespace OpenHub.Accessor.KeyCloak.Interfaces;

public interface IKeyCloakM2MTokenAccessor
{
    /// <summary>
    /// Gets a machine-to-machine access token for the given tenant realm via
    /// the client_credentials grant, cached until shortly before it expires.
    /// </summary>
    Task<string> GetTokenAsync(string realmSlug, CancellationToken ct = default);

    /// <summary>
    /// Evicts the cached token for realmSlug, forcing the next GetTokenAsync call to fetch a
    /// fresh one. For a caller that got a 401 from a downstream API using this token — the
    /// cache's fixed TTL is a conservative guess, not authoritative, so a 401 can still happen
    /// (e.g. a long-running caller holding a token across many requests past its real expiry).
    /// </summary>
    Task InvalidateTokenAsync(string realmSlug, CancellationToken ct = default);
}
