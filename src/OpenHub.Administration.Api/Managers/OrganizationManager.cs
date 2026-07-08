using OpenHub.Accessor.KeyCloak.Interfaces;
using OpenHub.Accessor.KeyCloak.Models;
using OpenHub.Administration.Api.Managers.Interfaces;
using OpenHub.Administration.Api.Managers.Models;
using OpenHub.Administration.Api.Mappers;

namespace OpenHub.Administration.Api.Managers;

public class OrganizationManager(
    IKeyCloakRealmAccessor realmAccessor,
    IKeyCloakOrganizationAccessor orgAccessor,
    OrganizationMapper mapper) : IOrganizationManager
{
    // Realms that exist for platform purposes but are never tenant organizations.
    private static readonly string[] NonTenantRealms = ["master", "platform-admin"];

    public async Task<OrganizationDto> CreateOrganizationAsync(CreateOrganizationDto request, CancellationToken ct = default)
    {
        var realm = await realmAccessor.GetRealmBySlugAsync(request.RealmSlug, ct)
            ?? throw new InvalidOperationException(
                $"Realm '{request.RealmSlug}' does not exist. Realm provisioning is a manual ops step — create the realm first.");

        var organizationId = await orgAccessor.CreateOrganizationAsync(realm.Realm, new CreateKeyCloakOrganizationRequest
        {
            Name = request.Name,
            Alias = request.Alias,
            Enabled = true,
            Domains = request.Domains
        }, ct);

        var created = await orgAccessor.GetOrganizationAsync(realm.Realm, organizationId, ct)
            ?? throw new InvalidOperationException("Organization was created but could not be re-fetched from Keycloak.");

        return mapper.ToDto(created, realm.Realm);
    }

    public async Task<OrganizationDto?> GetOrganizationAsync(string realmSlug, CancellationToken ct = default)
    {
        var orgs = await orgAccessor.ListOrganizationsAsync(realmSlug, ct);
        var org = orgs.FirstOrDefault();
        return org is null ? null : mapper.ToDto(org, realmSlug);
    }

    public async Task<IReadOnlyList<OrganizationDto>> ListOrganizationsAsync(CancellationToken ct = default)
    {
        var realms = await realmAccessor.ListRealmsAsync(ct);
        var results = new List<OrganizationDto>();

        foreach (var realm in realms.Where(r => !NonTenantRealms.Contains(r.Realm, StringComparer.OrdinalIgnoreCase)))
        {
            var orgs = await orgAccessor.ListOrganizationsAsync(realm.Realm, ct);
            results.AddRange(orgs.Select(o => mapper.ToDto(o, realm.Realm)));
        }

        return results;
    }
}
