namespace OpenHub.Administration.Api.Managers.Models;

public class CreateUserDto
{
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    /// <summary>Optional realm role to assign on creation — goes through the same escalation guard as AssignRoleAsync.</summary>
    public string? InitialRole { get; set; }
}
