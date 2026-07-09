using OpenHub.SalesOrg.Api.Accessors.Models;
using OpenHub.SalesOrg.Api.Managers.Models;
using OpenHub.SalesOrg.Api.Models.Requests;
using OpenHub.SalesOrg.Api.Models.Responses.V1;
using Riok.Mapperly.Abstractions;

namespace OpenHub.SalesOrg.Api.Mappers;

[Mapper]
public partial class SalesRoleMapper
{
    // Accessor -> Manager
    public partial SalesRoleDto ToDto(SalesRoleEntity entity);
    public partial IReadOnlyList<SalesRoleDto> ToDtoList(IEnumerable<SalesRoleEntity> entities);

    // Manager -> Accessor
    public partial SalesRoleEntity ToEntity(SalesRoleDto dto);

    // Controller -> Manager
    [MapperIgnoreTarget(nameof(SalesRoleDto.Id))]
    [MapperIgnoreTarget(nameof(SalesRoleDto.TenantId))]
    [MapperIgnoreTarget(nameof(SalesRoleDto.ActiveStatus))]
    [MapperIgnoreTarget(nameof(SalesRoleDto.CreatedAt))]
    [MapperIgnoreTarget(nameof(SalesRoleDto.UpdatedAt))]
    public partial SalesRoleDto ToDto(CreateSalesRoleRequest request);

    // Manager -> Controller
    [MapperIgnoreSource(nameof(SalesRoleDto.TenantId))]
    public partial SalesRoleResponse ToResponse(SalesRoleDto dto);

    public partial IReadOnlyList<SalesRoleResponse> ToResponseList(IEnumerable<SalesRoleDto> dtos);
}
