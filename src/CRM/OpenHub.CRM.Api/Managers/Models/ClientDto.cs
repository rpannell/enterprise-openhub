namespace OpenHub.CRM.Api.Managers.Models;

public class ClientDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SalesPersonId { get; set; }
    public string? SalesPersonName { get; set; }
    public string Name { get; set; } = null!;
    public string? LegalName { get; set; }
    public string? KeycloakOrganizationId { get; set; }
    public string? KeycloakOrganizationAlias { get; set; }
    public string Status { get; set; } = null!;
    public string? Industry { get; set; }
    public string? Website { get; set; }
    public string? Phone { get; set; }
    public decimal? AnnualRevenue { get; set; }
    public int? EmployeeCount { get; set; }
    public string? LeadSource { get; set; }
    public string AccountTier { get; set; } = null!;
    public DateOnly? FirstContactDate { get; set; }
    public DateTimeOffset? LastActivityAt { get; set; }
    public string? Notes { get; set; }
    public Guid CreateUserId { get; set; }
    public Guid ModifyUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public List<ClientAddressDto> Addresses { get; set; } = [];
    public List<ClientContactDto> Contacts { get; set; } = [];
}
