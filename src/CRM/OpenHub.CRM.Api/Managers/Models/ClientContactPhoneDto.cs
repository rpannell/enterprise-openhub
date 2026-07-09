namespace OpenHub.CRM.Api.Managers.Models;

public class ClientContactPhoneDto
{
    public Guid Id { get; set; }
    public Guid ContactId { get; set; }
    public string PhoneType { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public bool IsPrimary { get; set; }
}
