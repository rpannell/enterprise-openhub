namespace OpenHub.SalesOrg.Api.Managers.Models;

public class SalesPersonDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string? UniqueId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? Phone { get; set; }
    public string? JobTitle { get; set; }
    public string? EmployeeNumber { get; set; }
    public bool ActiveStatus { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
