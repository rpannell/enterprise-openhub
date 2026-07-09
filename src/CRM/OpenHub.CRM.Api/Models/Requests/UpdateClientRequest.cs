using System.ComponentModel;

namespace OpenHub.CRM.Api.Models.Requests;

// Patch semantics — only fields present in the request body are modified. Addresses/Contacts,
// when provided (non-null), fully replace the client's existing set; omit them to leave as-is.
public class UpdateClientRequest
{
    [Description("Display name of the client business.")]
    public string? Name { get; set; }

    [Description("Registered legal name.")]
    public string? LegalName { get; set; }

    [Description("The sales person responsible for this client.")]
    public Guid? SalesPersonId { get; set; }

    [Description("Lifecycle status: prospect, active, on_hold, or churned.")]
    public string? Status { get; set; }

    [Description("Industry the client operates in.")]
    public string? Industry { get; set; }

    [Description("Client's website URL.")]
    public string? Website { get; set; }

    [Description("Primary phone number.")]
    public string? Phone { get; set; }

    [Description("Estimated annual revenue.")]
    public decimal? AnnualRevenue { get; set; }

    [Description("Estimated employee count.")]
    public int? EmployeeCount { get; set; }

    [Description("How this client was acquired.")]
    public string? LeadSource { get; set; }

    [Description("Account tier: strategic, key, standard, or small.")]
    public string? AccountTier { get; set; }

    [Description("Date of first contact with this client.")]
    public DateOnly? FirstContactDate { get; set; }

    [Description("Free-text notes about this client.")]
    public string? Notes { get; set; }

    [Description("If provided, fully replaces the client's address list.")]
    public List<ClientAddressRequest>? Addresses { get; set; }

    [Description("If provided, fully replaces the client's contact list.")]
    public List<ClientContactRequest>? Contacts { get; set; }
}
