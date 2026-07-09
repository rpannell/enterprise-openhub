namespace OpenHub.Accessor.KeyCloak.Models;

public class UpdateKeyCloakUserRequest
{
    /// <summary>Always set equal to <see cref="Email"/> by the caller — this app has no separate username.</summary>
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool Enabled { get; set; }
}
