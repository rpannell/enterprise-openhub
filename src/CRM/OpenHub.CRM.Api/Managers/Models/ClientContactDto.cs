namespace OpenHub.CRM.Api.Managers.Models;

public class ClientContactDto
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string? Title { get; set; }
    public string? Email { get; set; }
    public bool IsPrimary { get; set; }
    public string? Notes { get; set; }
    public List<ClientContactPhoneDto> Phones { get; set; } = [];
}
