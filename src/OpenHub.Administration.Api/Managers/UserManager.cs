using System.Security.Cryptography;
using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.Accessor.KeyCloak.Models;
using OpenHub.Administration.Api.Managers.Engines.Interfaces;
using OpenHub.Administration.Api.Managers.Interfaces;
using OpenHub.Administration.Api.Managers.Models;
using OpenHub.Administration.Api.Mappers;

namespace OpenHub.Administration.Api.Managers;

public class UserManager(
    IKeyCloakUserAccessor userAccessor,
    IKeyCloakOrganizationAccessor orgAccessor,
    IRoleAssignmentEngine roleAssignmentEngine,
    UserMapper mapper,
    RoleMapper roleMapper) : IUserManager
{
    public async Task<IReadOnlyList<UserDto>> ListUsersAsync(AdminCallerContext caller, string realmSlug, CancellationToken ct = default)
    {
        EnsureCanAccessRealm(caller, realmSlug);

        var users = await userAccessor.ListUsersAsync(realmSlug, ct: ct);
        var results = new List<UserDto>(users.Count);
        foreach (var user in users)
        {
            var roles = await userAccessor.GetUserRealmRolesAsync(realmSlug, user.Id, ct);
            results.Add(mapper.ToDto(user, roles.Select(r => r.Name).ToList()));
        }

        return results;
    }

    public async Task<UserDto> CreateUserAsync(AdminCallerContext caller, string realmSlug, CreateUserDto request, CancellationToken ct = default)
    {
        EnsureCanAccessRealm(caller, realmSlug);

        var userId = await userAccessor.CreateUserAsync(realmSlug, new CreateKeyCloakUserRequest
        {
            Username = request.Username,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            TemporaryPassword = GenerateTemporaryPassword()
        }, ct);

        await TryAddToOrganizationAsync(realmSlug, userId, ct);

        var assignedRoles = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.InitialRole))
        {
            await AssignRoleAsync(caller, realmSlug, userId, request.InitialRole, ct);
            assignedRoles.Add(request.InitialRole);
        }

        var created = await userAccessor.GetUserByIdAsync(realmSlug, userId, ct)
            ?? throw new InvalidOperationException("User was created but could not be re-fetched from Keycloak.");

        return mapper.ToDto(created, assignedRoles);
    }

    public async Task<IReadOnlyList<RoleDto>> ListAssignableRolesAsync(AdminCallerContext caller, string realmSlug, CancellationToken ct = default)
    {
        EnsureCanAccessRealm(caller, realmSlug);

        var roles = await userAccessor.ListRealmRolesAsync(realmSlug, ct);
        var assignable = roles.Where(r => roleAssignmentEngine.Evaluate(r.Name, caller.IsPlatformAdmin).Allowed);

        return assignable.Select(roleMapper.ToDto).ToList();
    }

    public async Task AssignRoleAsync(AdminCallerContext caller, string realmSlug, string userId, string roleName, CancellationToken ct = default)
    {
        EnsureCanAccessRealm(caller, realmSlug);

        var decision = roleAssignmentEngine.Evaluate(roleName, caller.IsPlatformAdmin);
        if (!decision.Allowed)
            throw new ForbiddenOperationException(decision.DenyReason!);

        var role = await userAccessor.GetRealmRoleAsync(realmSlug, roleName, ct)
            ?? throw new InvalidOperationException($"Role '{roleName}' does not exist in realm '{realmSlug}'.");

        await userAccessor.AssignRealmRoleAsync(realmSlug, userId, role, ct);
    }

    private async Task TryAddToOrganizationAsync(string realmSlug, string userId, CancellationToken ct)
    {
        var orgs = await orgAccessor.ListOrganizationsAsync(realmSlug, ct);
        var org = orgs.FirstOrDefault();
        if (org is not null)
            await orgAccessor.AddMemberAsync(realmSlug, org.Id, userId, ct);
    }

    private static void EnsureCanAccessRealm(AdminCallerContext caller, string realmSlug)
    {
        if (!caller.IsPlatformAdmin && !string.Equals(caller.RealmSlug, realmSlug, StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenOperationException($"tenant-admin cannot manage organization '{realmSlug}'.");
    }

    private static string GenerateTemporaryPassword() =>
        $"Tmp-{Convert.ToHexString(RandomNumberGenerator.GetBytes(12))}!";
}
