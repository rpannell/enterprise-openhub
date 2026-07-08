namespace OpenHub.Accessor.KeyCloak.Models;

public class KeyCloakOrganizationInfo
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Alias { get; set; } = null!;
    public bool Enabled { get; set; }
    public IReadOnlyList<string> Domains { get; set; } = [];
}
