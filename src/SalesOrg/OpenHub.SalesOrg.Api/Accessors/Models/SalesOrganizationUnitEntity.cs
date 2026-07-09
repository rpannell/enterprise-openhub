namespace OpenHub.SalesOrg.Api.Accessors.Models;

public class SalesOrganizationUnitEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string OrganizationUnitType { get; set; } = null!;
    public Guid? ParentOrganizationUnitId { get; set; }
    public bool ActiveStatus { get; set; }
    public Guid CreateUserId { get; set; }
    public Guid ModifyUserId { get; set; }
    public Guid? DeleteUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
