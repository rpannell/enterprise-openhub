using OpenHub.SalesOrg.Api.Accessors.Models;
using OpenHub.SalesOrg.Api.Managers.Models;
using OpenHub.SalesOrg.Api.Models.Requests;
using OpenHub.SalesOrg.Api.Models.Responses.V1;
using Riok.Mapperly.Abstractions;

namespace OpenHub.SalesOrg.Api.Mappers;

[Mapper]
public partial class SalesOrganizationUnitMapper
{
    // Accessor -> Manager
    public partial SalesOrganizationUnitDto ToDto(SalesOrganizationUnitEntity entity);
    public partial IReadOnlyList<SalesOrganizationUnitDto> ToDtoList(IEnumerable<SalesOrganizationUnitEntity> entities);

    // Manager -> Accessor
    public partial SalesOrganizationUnitEntity ToEntity(SalesOrganizationUnitDto dto);

    // Controller -> Manager
    [MapperIgnoreTarget(nameof(SalesOrganizationUnitDto.Id))]
    [MapperIgnoreTarget(nameof(SalesOrganizationUnitDto.TenantId))]
    [MapperIgnoreTarget(nameof(SalesOrganizationUnitDto.ActiveStatus))]
    [MapperIgnoreTarget(nameof(SalesOrganizationUnitDto.CreateUserId))]
    [MapperIgnoreTarget(nameof(SalesOrganizationUnitDto.ModifyUserId))]
    [MapperIgnoreTarget(nameof(SalesOrganizationUnitDto.CreatedAt))]
    [MapperIgnoreTarget(nameof(SalesOrganizationUnitDto.UpdatedAt))]
    public partial SalesOrganizationUnitDto ToDto(CreateSalesOrganizationUnitRequest request);

    // Manager -> Controller
    [MapperIgnoreSource(nameof(SalesOrganizationUnitDto.TenantId))]
    [MapperIgnoreSource(nameof(SalesOrganizationUnitDto.CreateUserId))]
    [MapperIgnoreSource(nameof(SalesOrganizationUnitDto.ModifyUserId))]
    public partial SalesOrganizationUnitResponse ToResponse(SalesOrganizationUnitDto dto);

    public partial IReadOnlyList<SalesOrganizationUnitResponse> ToResponseList(IEnumerable<SalesOrganizationUnitDto> dtos);
}
