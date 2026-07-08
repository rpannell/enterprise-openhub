namespace OpenHub.Accessor.KeyCloak.Models;

public class CreateKeyCloakOrganizationRequest
{
    public string Name { get; set; } = null!;
    public string Alias { get; set; } = null!;
    public bool Enabled { get; set; } = true;
    public IReadOnlyList<string> Domains { get; set; } = [];
}
