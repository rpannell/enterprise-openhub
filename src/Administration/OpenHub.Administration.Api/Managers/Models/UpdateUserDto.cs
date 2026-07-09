namespace OpenHub.Administration.Api.Managers.Models;

public class UpdateUserDto
{
    public string Email { get; set; } = null!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool Enabled { get; set; }
}
