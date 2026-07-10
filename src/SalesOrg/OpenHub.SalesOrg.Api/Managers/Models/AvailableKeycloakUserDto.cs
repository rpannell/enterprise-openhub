namespace OpenHub.SalesOrg.Api.Managers.Models;

public class AvailableKeycloakUserDto
{
    public string UniqueId { get; set; } = null!;
    public string Username { get; set; } = null!;
    public string? Email { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
}
