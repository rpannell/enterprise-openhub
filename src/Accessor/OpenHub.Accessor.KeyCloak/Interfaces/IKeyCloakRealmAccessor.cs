using OpenHub.Accessor.KeyCloak.Models;

namespace OpenHub.Accessor.KeyCloak.Interfaces;

public interface IKeyCloakRealmAccessor
{
    Task<KeyCloakRealmInfo?> GetRealmBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>Lists every realm the admin credentials can see, including "master".</summary>
    Task<IReadOnlyList<KeyCloakRealmInfo>> ListRealmsAsync(CancellationToken ct = default);
}
