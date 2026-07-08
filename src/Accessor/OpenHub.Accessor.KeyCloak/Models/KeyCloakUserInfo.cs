namespace OpenHub.Accessor.KeyCloak.Models;

public class KeyCloakUserInfo
{
    public string Id { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool Enabled { get; set; }
}
