using OpenHub.Administration.Api.Managers.Models;

namespace OpenHub.Administration.Api.Managers.Engines.Interfaces;

public interface IRoleAssignmentEngine
{
    RoleAssignmentDecision Evaluate(string roleName, bool callerIsPlatformAdmin);
}
