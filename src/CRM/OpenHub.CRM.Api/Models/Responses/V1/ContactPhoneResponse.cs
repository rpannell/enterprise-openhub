using System.ComponentModel;

namespace OpenHub.CRM.Api.Models.Responses.V1;

public class ContactPhoneResponse
{
    [Description("Unique phone identifier.")]
    public Guid Id { get; set; }

    [Description("Phone kind: work, cell, home, or other.")]
    public string PhoneType { get; set; } = null!;

    [Description("The phone number.")]
    public string PhoneNumber { get; set; } = null!;

    [Description("Whether this is the contact's primary phone.")]
    public bool IsPrimary { get; set; }
}
