using System.ComponentModel;

namespace OpenHub.SalesOrg.Api.Models.Requests;

public class UpdateSalesRoleRequest
{
    [Description("Role name.")]
    public string? Name { get; set; }

    [Description("Description of this role's responsibilities.")]
    public string? Description { get; set; }

    [Description("Rank used to derive org-unit leadership and manager chains.")]
    public int? HierarchyLevel { get; set; }

    [Description("Whether this role is currently active/assignable.")]
    public bool? ActiveStatus { get; set; }
}
