using OpenHub.Accessor.KeyCloak.Models;
using OpenHub.Administration.Api.Managers.Models;
using OpenHub.Administration.Api.Models.Requests;
using OpenHub.Administration.Api.Models.Responses.V1;
using Riok.Mapperly.Abstractions;

namespace OpenHub.Administration.Api.Mappers;

[Mapper]
public partial class OrganizationMapper
{
    public partial CreateOrganizationDto ToDto(CreateOrganizationRequest request);

    public partial OrganizationResponse ToResponse(OrganizationDto dto);

    // Hand-written (not Mapperly-generated): realmSlug isn't a field on KeyCloakOrganizationInfo,
    // it's context the accessor call site already knows (which realm it queried).
    public OrganizationDto ToDto(KeyCloakOrganizationInfo info, string realmSlug) => new()
    {
        Id = info.Id,
        RealmSlug = realmSlug,
        Name = info.Name,
        Alias = info.Alias,
        Enabled = info.Enabled,
        Domains = info.Domains
    };
}
