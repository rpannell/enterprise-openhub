using OpenHub.Accessor.KeyCloak.Models;

namespace OpenHub.Accessor.KeyCloak.Interfaces;

public interface IKeyCloakRealmAccessor
{
    Task<KeyCloakRealmInfo?> GetRealmBySlugAsync(string slug, CancellationToken ct = default);
}
