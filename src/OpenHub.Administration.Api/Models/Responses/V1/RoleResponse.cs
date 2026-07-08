using System.ComponentModel;

namespace OpenHub.Administration.Api.Models.Responses.V1;

public class RoleResponse
{
    [Description("Realm role name.")]
    public string Name { get; set; } = null!;

    [Description("Role description, if set in Keycloak.")]
    public string? Description { get; set; }
}
