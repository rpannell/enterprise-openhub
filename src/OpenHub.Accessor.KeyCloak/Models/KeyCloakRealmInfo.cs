namespace OpenHub.Accessor.KeyCloak.Models;

public class KeyCloakRealmInfo
{
    public string Id { get; set; } = null!;
    public string Realm { get; set; } = null!;
    public string? DisplayName { get; set; }
    public bool Enabled { get; set; }
}
