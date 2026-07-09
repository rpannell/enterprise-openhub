using System.ComponentModel;

namespace OpenHub.CRM.Api.Models.Requests;

public class ClientContactRequest
{
    [Description("Contact's first name. Required.")]
    public string FirstName { get; set; } = null!;

    [Description("Contact's last name. Required.")]
    public string LastName { get; set; } = null!;

    [Description("Job title.")]
    public string? Title { get; set; }

    [Description("Contact email address.")]
    public string? Email { get; set; }

    [Description("Whether this is the primary contact for the client.")]
    public bool IsPrimary { get; set; }

    [Description("Free-text notes about this contact.")]
    public string? Notes { get; set; }

    [Description("The contact's phone numbers (work, cell, home, other). May be empty.")]
    public List<ContactPhoneRequest> Phones { get; set; } = [];
}
