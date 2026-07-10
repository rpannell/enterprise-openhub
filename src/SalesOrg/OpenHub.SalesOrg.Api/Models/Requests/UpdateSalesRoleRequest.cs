using System.ComponentModel;

namespace OpenHub.SalesOrg.Api.Models.Requests;

public class UpdateSalesRoleRequest
{
    [Description("Role name.")]
    public string? Name { get; set; }

    [Description("Description of this role's responsibilities.")]
    public string? Description { get; set; }

    [Description("Reparents this role under a new parent, recomputing its hierarchy level (and cascading to any of its own child roles). Rejected if the new parent is this role itself or one of its own descendants. Pass null explicitly is not supported by patch semantics — omit the field to leave unchanged.")]
    public Guid? ParentRoleId { get; set; }

    [Description("Whether this role is currently active/assignable.")]
    public bool? ActiveStatus { get; set; }
}
