using System.ComponentModel;

namespace OpenHub.SalesOrg.Api.Models.Responses.V1;

public class SalesPersonResponse
{
    [Description("Unique sales person identifier.")]
    public Guid Id { get; set; }

    [Description("Keycloak user id (sub claim) this sales person is linked to, if any.")]
    public string? UniqueId { get; set; }

    [Description("First name.")]
    public string FirstName { get; set; } = null!;

    [Description("Last name.")]
    public string LastName { get; set; } = null!;

    [Description("Email address.")]
    public string Email { get; set; } = null!;

    [Description("Phone number.")]
    public string? Phone { get; set; }

    [Description("Job title.")]
    public string? JobTitle { get; set; }

    [Description("Internal employee number, if tracked.")]
    public string? EmployeeNumber { get; set; }

    [Description("Whether this person is currently active.")]
    public bool ActiveStatus { get; set; }

    [Description("Date this person started in a sales role.")]
    public DateOnly? StartDate { get; set; }

    [Description("Date this person left/stopped in a sales role.")]
    public DateOnly? EndDate { get; set; }

    [Description("Timestamp when the record was created (UTC).")]
    public DateTimeOffset CreatedAt { get; set; }

    [Description("Timestamp when the record was last modified (UTC).")]
    public DateTimeOffset UpdatedAt { get; set; }
}
