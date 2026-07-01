namespace OpenHub.Accessor.KeyCloak.Interfaces;

public interface IKeyCloakM2MTokenAccessor
{
    /// <summary>
    /// Gets a machine-to-machine access token for the given tenant realm via
    /// the client_credentials grant, cached until shortly before it expires.
    /// </summary>
    Task<string> GetTokenAsync(string realmSlug, CancellationToken ct = default);
}
