using System.ComponentModel;

namespace OpenHub.SalesOrg.Api.Models.Requests;

public class CreateSalesAssignmentRequest
{
    [Description("The sales person being assigned. Required.")]
    public Guid SalesPersonId { get; set; }

    [Description("The org unit the person is being assigned into. Required.")]
    public Guid OrganizationUnitId { get; set; }

    [Description("The role the person holds in this unit. Required.")]
    public Guid RoleId { get; set; }

    [Description("Date this assignment takes effect. Required.")]
    public DateOnly EffectiveStartDate { get; set; }

    [Description("Date this assignment ends. Omit/null for an open-ended (current) assignment.")]
    public DateOnly? EffectiveEndDate { get; set; }

    [Description("Whether this is the person's primary (home) assignment. Creating a new active+primary assignment automatically closes out any prior active+primary assignment for the same person — at most one may be active at a time. Defaults to true.")]
    public bool PrimaryAssignmentFlag { get; set; } = true;
}
