namespace OpenHub.SalesOrg.Api.Managers.Models;

public class SalesAssignmentDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SalesPersonId { get; set; }
    public string? SalesPersonName { get; set; }
    public Guid OrganizationUnitId { get; set; }
    public string? OrganizationUnitName { get; set; }
    public Guid RoleId { get; set; }
    public string? RoleName { get; set; }
    public DateOnly EffectiveStartDate { get; set; }
    public DateOnly? EffectiveEndDate { get; set; }
    public bool PrimaryAssignmentFlag { get; set; }
    public bool ActiveStatus { get; set; }
    public Guid CreateUserId { get; set; }
    public Guid ModifyUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
