using System.ComponentModel;

namespace OpenHub.SalesOrg.Api.Models.Requests;

public class CreateSalesRoleRequest
{
    [Description("Role name (e.g. \"Territory Manager\"). Required, unique per tenant not enforced — tenant-configurable.")]
    public string Name { get; set; } = null!;

    [Description("Description of this role's responsibilities.")]
    public string? Description { get; set; }

    [Description("Rank used to derive org-unit leadership and manager chains — higher outranks lower within the same org unit. Optional; a role with no level never outranks anyone.")]
    public int? HierarchyLevel { get; set; }
}
