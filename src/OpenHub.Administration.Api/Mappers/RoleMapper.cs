using OpenHub.Accessor.KeyCloak.Models;
using OpenHub.Administration.Api.Managers.Models;
using OpenHub.Administration.Api.Models.Responses.V1;
using Riok.Mapperly.Abstractions;

namespace OpenHub.Administration.Api.Mappers;

[Mapper]
public partial class RoleMapper
{
    [MapperIgnoreSource(nameof(KeyCloakRoleInfo.Id))]
    public partial RoleDto ToDto(KeyCloakRoleInfo info);

    public partial RoleResponse ToResponse(RoleDto dto);
}
