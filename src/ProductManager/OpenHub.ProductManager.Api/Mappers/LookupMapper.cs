using OpenHub.ProductManager.Api.Accessors.Models;
using OpenHub.ProductManager.Api.Managers.Models;
using OpenHub.ProductManager.Api.Models.Responses.V1;
using Riok.Mapperly.Abstractions;

namespace OpenHub.ProductManager.Api.Mappers;

[Mapper]
public partial class LookupMapper
{
    // Accessor → Manager (ref.* rows)
    public partial LookupItemDto ToDto(LookupItemRow row);
    public partial List<LookupItemDto> ToDtoList(IReadOnlyList<LookupItemRow> rows);

    public partial UomLookupItemDto ToDto(UomLookupRow row);
    public partial List<UomLookupItemDto> ToDtoList(IReadOnlyList<UomLookupRow> rows);

    public partial CurrencyLookupItemDto ToDto(CurrencyLookupRow row);
    public partial List<CurrencyLookupItemDto> ToDtoList(IReadOnlyList<CurrencyLookupRow> rows);

    // Accessor → Manager (tenant-specific catalog entities)
    [MapperIgnoreSource("TenantId")]
    [MapperIgnoreSource("CountryCode")]
    [MapperIgnoreSource("WebsiteUrl")]
    [MapperIgnoreSource("IsActive")]
    [MapperIgnoreSource("CreatedAt")]
    [MapperIgnoreSource("UpdatedAt")]
    [MapperIgnoreSource("DeletedAt")]
    public partial LookupItemDto ToDto(ManufacturerEntity entity);
    public partial List<LookupItemDto> ToDtoList(IReadOnlyList<ManufacturerEntity> entities);

    [MapperIgnoreSource("TenantId")]
    [MapperIgnoreSource("LogoUrl")]
    [MapperIgnoreSource("IsActive")]
    [MapperIgnoreSource("CreatedAt")]
    [MapperIgnoreSource("UpdatedAt")]
    [MapperIgnoreSource("DeletedAt")]
    public partial BrandLookupItemDto ToDto(BrandEntity entity);
    public partial List<BrandLookupItemDto> ToDtoList(IReadOnlyList<BrandEntity> entities);

    [MapperIgnoreSource("TenantId")]
    [MapperIgnoreSource("ManufacturerId")]
    [MapperIgnoreSource("LogoUrl")]
    [MapperIgnoreSource("IsActive")]
    [MapperIgnoreSource("CreatedAt")]
    [MapperIgnoreSource("UpdatedAt")]
    [MapperIgnoreSource("DeletedAt")]
    public partial LookupItemDto ToLookupDto(BrandEntity entity);

    // Manager → Controller
    public partial LookupItemResponse ToResponse(LookupItemDto dto);
    public partial List<LookupItemResponse> ToResponseList(List<LookupItemDto> dtos);

    public partial UomLookupItemResponse ToResponse(UomLookupItemDto dto);
    public partial List<UomLookupItemResponse> ToResponseList(List<UomLookupItemDto> dtos);

    public partial CurrencyLookupItemResponse ToResponse(CurrencyLookupItemDto dto);
    public partial List<CurrencyLookupItemResponse> ToResponseList(List<CurrencyLookupItemDto> dtos);

    public partial BrandLookupItemResponse ToResponse(BrandLookupItemDto dto);
    public partial List<BrandLookupItemResponse> ToResponseList(List<BrandLookupItemDto> dtos);

    public partial LookupsResponse ToResponse(LookupsDto dto);
}
