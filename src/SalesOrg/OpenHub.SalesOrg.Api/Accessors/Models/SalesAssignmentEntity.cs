namespace OpenHub.SalesOrg.Api.Accessors.Models;

public class SalesAssignmentEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid SalesPersonId { get; set; }
    public Guid OrganizationUnitId { get; set; }
    public Guid RoleId { get; set; }
    public DateOnly EffectiveStartDate { get; set; }
    public DateOnly? EffectiveEndDate { get; set; }
    public bool PrimaryAssignmentFlag { get; set; }
    public bool ActiveStatus { get; set; }
    public Guid CreateUserId { get; set; }
    public Guid ModifyUserId { get; set; }
    public Guid? DeleteUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
