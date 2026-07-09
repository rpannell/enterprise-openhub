using System.ComponentModel;

namespace OpenHub.SalesOrg.Api.Models.Requests;

public class CreateSalesOrganizationUnitRequest
{
    [Description("Unit name (e.g. \"Northeast Territory\"). Required.")]
    public string Name { get; set; } = null!;

    [Description("Description of this unit's scope.")]
    public string? Description { get; set; }

    [Description("App-level unit type, e.g. territory, district, region, global. Free text — not a fixed enum, so new types don't require a migration.")]
    public string OrganizationUnitType { get; set; } = null!;

    [Description("The parent unit this unit reports into, if any. Omit for a root/top-level unit.")]
    public Guid? ParentOrganizationUnitId { get; set; }
}
