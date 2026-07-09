using System.ComponentModel;

namespace OpenHub.SalesOrg.Api.Models.Responses.V1;

public class SalesRoleResponse
{
    [Description("Unique sales role identifier.")]
    public Guid Id { get; set; }

    [Description("Role name.")]
    public string Name { get; set; } = null!;

    [Description("Description of this role's responsibilities.")]
    public string? Description { get; set; }

    [Description("Rank used to derive org-unit leadership and manager chains.")]
    public int? HierarchyLevel { get; set; }

    [Description("Whether this role is currently active/assignable.")]
    public bool ActiveStatus { get; set; }

    [Description("Timestamp when the record was created (UTC).")]
    public DateTimeOffset CreatedAt { get; set; }

    [Description("Timestamp when the record was last modified (UTC).")]
    public DateTimeOffset UpdatedAt { get; set; }
}
