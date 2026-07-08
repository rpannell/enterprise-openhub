using OpenHub.Accessor.KeyCloak.Models;

namespace OpenHub.Accessor.KeyCloak.Interfaces;

/// <summary>
/// Wraps Keycloak's native Organizations feature (Admin REST API, realm-scoped) —
/// raw I/O only, no business rules. See <c>OpenHub.Administration.Api</c> for the
/// authorization/orchestration layer built on top of this.
/// </summary>
public interface IKeyCloakOrganizationAccessor
{
    /// <summary>Creates an Organization inside an already-provisioned realm. Returns the new organization's id.</summary>
    /// <exception cref="KeyCloakAdminConflictException">The realm already has an organization with this alias/domain.</exception>
    Task<string> CreateOrganizationAsync(
        string realmSlug, CreateKeyCloakOrganizationRequest request, CancellationToken ct = default);

    Task<KeyCloakOrganizationInfo?> GetOrganizationAsync(
        string realmSlug, string organizationId, CancellationToken ct = default);

    Task<IReadOnlyList<KeyCloakOrganizationInfo>> ListOrganizationsAsync(
        string realmSlug, CancellationToken ct = default);

    Task AddMemberAsync(
        string realmSlug, string organizationId, string userId, CancellationToken ct = default);

    Task<IReadOnlyList<KeyCloakUserInfo>> ListMembersAsync(
        string realmSlug, string organizationId, CancellationToken ct = default);
}
