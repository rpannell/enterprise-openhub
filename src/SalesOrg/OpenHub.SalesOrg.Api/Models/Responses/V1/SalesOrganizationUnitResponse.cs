using System.ComponentModel;

namespace OpenHub.SalesOrg.Api.Models.Responses.V1;

public class SalesOrganizationUnitResponse
{
    [Description("Unique organization unit identifier.")]
    public Guid Id { get; set; }

    [Description("Unit name.")]
    public string Name { get; set; } = null!;

    [Description("Description of this unit's scope.")]
    public string? Description { get; set; }

    [Description("App-level unit type, e.g. territory, district, region, global.")]
    public string OrganizationUnitType { get; set; } = null!;

    [Description("The parent unit this unit reports into, if any.")]
    public Guid? ParentOrganizationUnitId { get; set; }

    [Description("Whether this unit is currently active.")]
    public bool ActiveStatus { get; set; }

    [Description("Timestamp when the record was created (UTC).")]
    public DateTimeOffset CreatedAt { get; set; }

    [Description("Timestamp when the record was last modified (UTC).")]
    public DateTimeOffset UpdatedAt { get; set; }
}
