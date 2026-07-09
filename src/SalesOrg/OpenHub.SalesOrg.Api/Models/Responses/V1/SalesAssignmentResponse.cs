using System.ComponentModel;

namespace OpenHub.SalesOrg.Api.Models.Responses.V1;

public class SalesAssignmentResponse
{
    [Description("Unique assignment identifier.")]
    public Guid Id { get; set; }

    [Description("The assigned sales person.")]
    public Guid SalesPersonId { get; set; }

    [Description("Display name of the assigned sales person.")]
    public string? SalesPersonName { get; set; }

    [Description("The org unit this assignment is into.")]
    public Guid OrganizationUnitId { get; set; }

    [Description("Name of the org unit.")]
    public string? OrganizationUnitName { get; set; }

    [Description("The role held in this assignment.")]
    public Guid RoleId { get; set; }

    [Description("Name of the role.")]
    public string? RoleName { get; set; }

    [Description("Date this assignment takes/took effect.")]
    public DateOnly EffectiveStartDate { get; set; }

    [Description("Date this assignment ends, if closed. Null means current/open-ended.")]
    public DateOnly? EffectiveEndDate { get; set; }

    [Description("Whether this is the person's primary (home) assignment.")]
    public bool PrimaryAssignmentFlag { get; set; }

    [Description("Whether this assignment is currently active.")]
    public bool ActiveStatus { get; set; }

    [Description("Timestamp when the record was created (UTC).")]
    public DateTimeOffset CreatedAt { get; set; }

    [Description("Timestamp when the record was last modified (UTC).")]
    public DateTimeOffset UpdatedAt { get; set; }
}
