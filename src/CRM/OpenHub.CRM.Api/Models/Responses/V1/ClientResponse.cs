using System.ComponentModel;

namespace OpenHub.CRM.Api.Models.Responses.V1;

public class ClientResponse
{
    [Description("Unique client identifier.")]
    public Guid Id { get; set; }

    [Description("Display name of the client business.")]
    public string Name { get; set; } = null!;

    [Description("Registered legal name, if different from the display name.")]
    public string? LegalName { get; set; }

    [Description("The Keycloak Organization id created for this client, once provisioned.")]
    public string? KeycloakOrganizationId { get; set; }

    [Description("The sales person responsible for this client.")]
    public Guid SalesPersonId { get; set; }

    [Description("Display name of the responsible sales person.")]
    public string? SalesPersonName { get; set; }

    [Description("Lifecycle status: prospect, active, on_hold, or churned.")]
    public string Status { get; set; } = null!;

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
    public string AccountTier { get; set; } = null!;

    [Description("Date of first contact with this client.")]
    public DateOnly? FirstContactDate { get; set; }

    [Description("Timestamp of the client's most recent recorded activity (UTC).")]
    public DateTimeOffset? LastActivityAt { get; set; }

    [Description("Free-text notes about this client.")]
    public string? Notes { get; set; }

    [Description("Timestamp when the client was created (UTC).")]
    public DateTimeOffset CreatedAt { get; set; }

    [Description("Timestamp when the client was last modified (UTC).")]
    public DateTimeOffset UpdatedAt { get; set; }

    [Description("The client's addresses.")]
    public List<ClientAddressResponse> Addresses { get; set; } = [];

    [Description("The people at this client.")]
    public List<ClientContactResponse> Contacts { get; set; } = [];
}
