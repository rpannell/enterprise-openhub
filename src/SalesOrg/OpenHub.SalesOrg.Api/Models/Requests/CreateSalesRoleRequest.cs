using System.ComponentModel;

namespace OpenHub.SalesOrg.Api.Models.Requests;

public class CreateSalesRoleRequest
{
    [Description("Role name (e.g. \"Territory Manager\"). Required, unique per tenant not enforced — tenant-configurable.")]
    public string Name { get; set; } = null!;

    [Description("Description of this role's responsibilities.")]
    public string? Description { get; set; }

    [Description("The role this one reports up to (e.g. Sales Rep's parent is District Manager). Determines rank automatically — the new role's hierarchy level is set to the parent's level minus 100. Omit for a top-level role that outranks everyone below it.")]
    public Guid? ParentRoleId { get; set; }
}
