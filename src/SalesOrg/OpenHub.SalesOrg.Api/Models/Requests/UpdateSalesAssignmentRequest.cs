using System.ComponentModel;

namespace OpenHub.SalesOrg.Api.Models.Requests;

public class UpdateSalesAssignmentRequest
{
    [Description("Date this assignment ends. Set to close out an assignment without deleting it.")]
    public DateOnly? EffectiveEndDate { get; set; }

    [Description("Whether this is the person's primary (home) assignment.")]
    public bool? PrimaryAssignmentFlag { get; set; }

    [Description("Whether this assignment is currently active.")]
    public bool? ActiveStatus { get; set; }
}
