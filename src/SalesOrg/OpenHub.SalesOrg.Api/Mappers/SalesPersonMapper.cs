using OpenHub.SalesOrg.Api.Accessors.Models;
using OpenHub.SalesOrg.Api.Managers.Models;
using OpenHub.SalesOrg.Api.Models.Requests;
using OpenHub.SalesOrg.Api.Models.Responses.V1;
using Riok.Mapperly.Abstractions;

namespace OpenHub.SalesOrg.Api.Mappers;

[Mapper]
public partial class SalesPersonMapper
{
    // Accessor -> Manager
    public partial SalesPersonDto ToDto(SalesPersonEntity entity);
    public partial IReadOnlyList<SalesPersonDto> ToDtoList(IEnumerable<SalesPersonEntity> entities);

    // Manager -> Accessor
    public partial SalesPersonEntity ToEntity(SalesPersonDto dto);

    // Controller -> Manager
    [MapperIgnoreTarget(nameof(SalesPersonDto.Id))]
    [MapperIgnoreTarget(nameof(SalesPersonDto.TenantId))]
    [MapperIgnoreTarget(nameof(SalesPersonDto.ActiveStatus))]
    [MapperIgnoreTarget(nameof(SalesPersonDto.EndDate))]
    [MapperIgnoreTarget(nameof(SalesPersonDto.CreatedAt))]
    [MapperIgnoreTarget(nameof(SalesPersonDto.UpdatedAt))]
    public partial SalesPersonDto ToDto(CreateSalesPersonRequest request);

    // Manager -> Controller
    [MapperIgnoreSource(nameof(SalesPersonDto.TenantId))]
    public partial SalesPersonResponse ToResponse(SalesPersonDto dto);

    public partial IReadOnlyList<SalesPersonResponse> ToResponseList(IEnumerable<SalesPersonDto> dtos);
}
