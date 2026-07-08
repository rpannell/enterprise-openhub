using System.ComponentModel;

namespace OpenHub.Administration.Api.Models.Requests;

public class AssignRoleRequest
{
    [Description("Name of the realm role to assign. \"tenant-admin\" and \"platform-admin\" can only be granted by a platform-admin caller.")]
    public string RoleName { get; set; } = null!;
}
