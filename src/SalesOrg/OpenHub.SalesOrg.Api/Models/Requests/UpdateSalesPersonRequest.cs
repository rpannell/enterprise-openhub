using System.ComponentModel;

namespace OpenHub.SalesOrg.Api.Models.Requests;

public class UpdateSalesPersonRequest
{
    [Description("First name.")]
    public string? FirstName { get; set; }

    [Description("Last name.")]
    public string? LastName { get; set; }

    [Description("Email address.")]
    public string? Email { get; set; }

    [Description("Phone number.")]
    public string? Phone { get; set; }

    [Description("Job title.")]
    public string? JobTitle { get; set; }

    [Description("Internal employee number, if tracked.")]
    public string? EmployeeNumber { get; set; }

    [Description("Whether this person is currently active.")]
    public bool? ActiveStatus { get; set; }

    [Description("Date this person started in a sales role.")]
    public DateOnly? StartDate { get; set; }

    [Description("Date this person left/stopped in a sales role.")]
    public DateOnly? EndDate { get; set; }
}
