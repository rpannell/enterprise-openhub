using System.ComponentModel;

namespace OpenHub.SalesOrg.Api.Models.Requests;

public class UpdateSalesOrganizationUnitRequest
{
    [Description("Unit name.")]
    public string? Name { get; set; }

    [Description("Description of this unit's scope.")]
    public string? Description { get; set; }

    [Description("App-level unit type, e.g. territory, district, region, global.")]
    public string? OrganizationUnitType { get; set; }

    [Description("Reparents this unit under a new parent. Rejected if the new parent is this unit itself or one of its own descendants (would create a cycle). Pass null explicitly is not supported by patch semantics — omit the field to leave unchanged.")]
    public Guid? ParentOrganizationUnitId { get; set; }

    [Description("Whether this unit is currently active.")]
    public bool? ActiveStatus { get; set; }
}
