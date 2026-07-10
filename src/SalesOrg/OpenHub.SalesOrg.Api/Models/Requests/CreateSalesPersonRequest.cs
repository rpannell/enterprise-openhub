using System.ComponentModel;

namespace OpenHub.SalesOrg.Api.Models.Requests;

public class CreateSalesPersonRequest
{
    [Description("Keycloak user id (sub claim) of the identity this sales person is linked to. Required — sales people must already exist as a Keycloak user so they can log in.")]
    public string UniqueId { get; set; } = null!;

    [Description("First name. Required.")]
    public string FirstName { get; set; } = null!;

    [Description("Last name. Required.")]
    public string LastName { get; set; } = null!;

    [Description("Email address. Required.")]
    public string Email { get; set; } = null!;

    [Description("Phone number.")]
    public string? Phone { get; set; }

    [Description("Job title.")]
    public string? JobTitle { get; set; }

    [Description("Internal employee number, if tracked.")]
    public string? EmployeeNumber { get; set; }

    [Description("Date this person started in a sales role.")]
    public DateOnly? StartDate { get; set; }
}
