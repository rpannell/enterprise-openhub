namespace OpenHub.Administration.Api.Managers.Models;

public class UserDto
{
    public string Id { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool Enabled { get; set; }
    public IReadOnlyList<string> Roles { get; set; } = [];

    /// <summary>Only set by CreateUserAsync's return value — there's no SMTP integration yet, so this is the one time it's ever surfaced.</summary>
    public string? TemporaryPassword { get; set; }
}
