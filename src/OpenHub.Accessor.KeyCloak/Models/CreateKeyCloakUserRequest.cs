namespace OpenHub.Accessor.KeyCloak.Models;

public class CreateKeyCloakUserRequest
{
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    /// <summary>Temporary password set on creation — the user must change it on first login.</summary>
    public string TemporaryPassword { get; set; } = null!;
}
