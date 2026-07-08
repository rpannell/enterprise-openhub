using OpenHub.Administration.Api.Managers.Models;

namespace OpenHub.Administration.Api.Managers.Interfaces;

public interface IUserManager
{
    Task<IReadOnlyList<UserDto>> ListUsersAsync(AdminCallerContext caller, string realmSlug, CancellationToken ct = default);

    Task<UserDto> CreateUserAsync(AdminCallerContext caller, string realmSlug, CreateUserDto request, CancellationToken ct = default);

    Task<IReadOnlyList<RoleDto>> ListAssignableRolesAsync(AdminCallerContext caller, string realmSlug, CancellationToken ct = default);

    Task AssignRoleAsync(AdminCallerContext caller, string realmSlug, string userId, string roleName, CancellationToken ct = default);
}
