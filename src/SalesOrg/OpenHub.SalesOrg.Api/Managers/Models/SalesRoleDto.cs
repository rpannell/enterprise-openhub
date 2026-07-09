namespace OpenHub.SalesOrg.Api.Managers.Models;

public class SalesRoleDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int? HierarchyLevel { get; set; }
    public bool ActiveStatus { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
