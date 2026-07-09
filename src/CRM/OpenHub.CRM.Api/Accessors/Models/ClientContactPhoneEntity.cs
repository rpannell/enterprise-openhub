namespace OpenHub.CRM.Api.Accessors.Models;

public class ClientContactPhoneEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ContactId { get; set; }
    public string PhoneType { get; set; } = null!;
    public string PhoneNumber { get; set; } = null!;
    public bool IsPrimary { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
