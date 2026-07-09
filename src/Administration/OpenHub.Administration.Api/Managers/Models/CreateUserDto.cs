namespace OpenHub.Administration.Api.Managers.Models;

public class CreateUserDto
{
    public string Email { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }

    /// <summary>Realm roles to assign on creation — each goes through the same escalation guard as AssignRoleAsync.</summary>
    public List<string> InitialRoles { get; set; } = [];
}
