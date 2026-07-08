using OpenHub.Administration.Api.Managers.Engines.Interfaces;
using OpenHub.Administration.Api.Managers.Models;

namespace OpenHub.Administration.Api.Managers.Engines;

/// <summary>
/// Pure privilege-escalation guard — no I/O, no constructor dependencies. A tenant-admin
/// may assign any ordinary realm role but never "tenant-admin" or "platform-admin" to
/// anyone, including themselves; only a platform-admin can grant those.
/// </summary>
public sealed class RoleAssignmentEngine : IRoleAssignmentEngine
{
    private static readonly string[] ProtectedRoles = ["tenant-admin", "platform-admin"];

    public RoleAssignmentDecision Evaluate(string roleName, bool callerIsPlatformAdmin)
        => ProtectedRoles.Contains(roleName, StringComparer.OrdinalIgnoreCase) && !callerIsPlatformAdmin
            ? new RoleAssignmentDecision(false, $"Only a platform-admin can grant the '{roleName}' role.")
            : new RoleAssignmentDecision(true, null);
}
