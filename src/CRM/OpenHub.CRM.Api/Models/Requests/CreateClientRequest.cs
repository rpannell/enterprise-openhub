using System.ComponentModel;

namespace OpenHub.CRM.Api.Models.Requests;

public class CreateClientRequest
{
    [Description("Display name of the client business. Required, max 500 characters.")]
    public string Name { get; set; } = null!;

    [Description("Registered legal name, if different from the display name.")]
    public string? LegalName { get; set; }

    [Description("The sales person responsible for this client. Required.")]
    public Guid SalesPersonId { get; set; }

    [Description("Lifecycle status: prospect, active, on_hold, or churned. Defaults to prospect.")]
    public string Status { get; set; } = "prospect";

    [Description("Industry the client operates in.")]
    public string? Industry { get; set; }

    [Description("Client's website URL.")]
    public string? Website { get; set; }

    [Description("Primary phone number.")]
    public string? Phone { get; set; }

    [Description("Estimated annual revenue, for reporting/segmentation.")]
    public decimal? AnnualRevenue { get; set; }

    [Description("Estimated employee count, for reporting/segmentation.")]
    public int? EmployeeCount { get; set; }

    [Description("How this client was acquired (e.g. referral, inbound, outbound), for reporting.")]
    public string? LeadSource { get; set; }

    [Description("Account tier: strategic, key, standard, or small. Defaults to standard.")]
    public string AccountTier { get; set; } = "standard";

    [Description("Date of first contact with this client, for reporting.")]
    public DateOnly? FirstContactDate { get; set; }

    [Description("Free-text notes about this client.")]
    public string? Notes { get; set; }

    [Description("The client's addresses (billing, shipping, headquarters, etc). May be empty.")]
    public List<ClientAddressRequest> Addresses { get; set; } = [];

    [Description("The people at this client. May be empty.")]
    public List<ClientContactRequest> Contacts { get; set; } = [];
}
