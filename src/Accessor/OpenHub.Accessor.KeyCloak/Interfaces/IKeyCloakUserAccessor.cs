using OpenHub.Accessor.KeyCloak.Models;

namespace OpenHub.Accessor.KeyCloak.Interfaces;

/// <summary>
/// Wraps Keycloak's realm-scoped user and realm-role Admin REST endpoints — raw I/O
/// only, no business rules. See <c>OpenHub.Administration.Api</c> for the
/// authorization/orchestration layer built on top of this.
/// </summary>
public interface IKeyCloakUserAccessor
{
    /// <summary>Creates a user in the given realm. Returns the new user's id.</summary>
    /// <exception cref="KeyCloakAdminConflictException">The realm already has a user with this username/email.</exception>
    Task<string> CreateUserAsync(
        string realmSlug, CreateKeyCloakUserRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<KeyCloakUserInfo>> ListUsersAsync(
        string realmSlug, string? search = null, CancellationToken ct = default);

    Task<KeyCloakUserInfo?> GetUserByIdAsync(
        string realmSlug, string userId, CancellationToken ct = default);

    Task<IReadOnlyList<KeyCloakRoleInfo>> ListRealmRolesAsync(
        string realmSlug, CancellationToken ct = default);

    Task<KeyCloakRoleInfo?> GetRealmRoleAsync(
        string realmSlug, string roleName, CancellationToken ct = default);

    Task AssignRealmRoleAsync(
        string realmSlug, string userId, KeyCloakRoleInfo role, CancellationToken ct = default);

    Task<IReadOnlyList<KeyCloakRoleInfo>> GetUserRealmRolesAsync(
        string realmSlug, string userId, CancellationToken ct = default);
}
