using System.ComponentModel;

namespace OpenHub.CRM.Api.Models.Requests;

public class ContactPhoneRequest
{
    [Description("Phone kind: work, cell, home, or other. Defaults to work.")]
    public string PhoneType { get; set; } = "work";

    [Description("The phone number. Required.")]
    public string PhoneNumber { get; set; } = null!;

    [Description("Whether this is the contact's primary phone.")]
    public bool IsPrimary { get; set; }
}
