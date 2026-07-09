using OpenHub.SalesOrg.Api.Accessors.Models;
using OpenHub.SalesOrg.Api.Managers.Models;
using OpenHub.SalesOrg.Api.Models.Requests;
using OpenHub.SalesOrg.Api.Models.Responses.V1;
using Riok.Mapperly.Abstractions;

namespace OpenHub.SalesOrg.Api.Mappers;

[Mapper]
public partial class SalesAssignmentMapper
{
    // Accessor -> Manager
    [MapperIgnoreTarget(nameof(SalesAssignmentDto.SalesPersonName))]
    [MapperIgnoreTarget(nameof(SalesAssignmentDto.OrganizationUnitName))]
    [MapperIgnoreTarget(nameof(SalesAssignmentDto.RoleName))]
    public partial SalesAssignmentDto ToDto(SalesAssignmentEntity entity);

    public partial IReadOnlyList<SalesAssignmentDto> ToDtoList(IEnumerable<SalesAssignmentEntity> entities);

    // Manager -> Accessor
    [MapperIgnoreSource(nameof(SalesAssignmentDto.SalesPersonName))]
    [MapperIgnoreSource(nameof(SalesAssignmentDto.OrganizationUnitName))]
    [MapperIgnoreSource(nameof(SalesAssignmentDto.RoleName))]
    public partial SalesAssignmentEntity ToEntity(SalesAssignmentDto dto);

    // Controller -> Manager
    [MapperIgnoreTarget(nameof(SalesAssignmentDto.Id))]
    [MapperIgnoreTarget(nameof(SalesAssignmentDto.TenantId))]
    [MapperIgnoreTarget(nameof(SalesAssignmentDto.SalesPersonName))]
    [MapperIgnoreTarget(nameof(SalesAssignmentDto.OrganizationUnitName))]
    [MapperIgnoreTarget(nameof(SalesAssignmentDto.RoleName))]
    [MapperIgnoreTarget(nameof(SalesAssignmentDto.ActiveStatus))]
    [MapperIgnoreTarget(nameof(SalesAssignmentDto.CreateUserId))]
    [MapperIgnoreTarget(nameof(SalesAssignmentDto.ModifyUserId))]
    [MapperIgnoreTarget(nameof(SalesAssignmentDto.CreatedAt))]
    [MapperIgnoreTarget(nameof(SalesAssignmentDto.UpdatedAt))]
    public partial SalesAssignmentDto ToDto(CreateSalesAssignmentRequest request);

    // Manager -> Controller
    [MapperIgnoreSource(nameof(SalesAssignmentDto.TenantId))]
    [MapperIgnoreSource(nameof(SalesAssignmentDto.CreateUserId))]
    [MapperIgnoreSource(nameof(SalesAssignmentDto.ModifyUserId))]
    public partial SalesAssignmentResponse ToResponse(SalesAssignmentDto dto);

    public partial IReadOnlyList<SalesAssignmentResponse> ToResponseList(IEnumerable<SalesAssignmentDto> dtos);
}
