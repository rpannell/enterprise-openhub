using OpenHub.ProductManager.Api.Accessors.Models;
using OpenHub.ProductManager.Api.Managers.Models;
using OpenHub.ProductManager.Api.Models.Requests;
using OpenHub.ProductManager.Api.Models.Responses.V1;
using Riok.Mapperly.Abstractions;

namespace OpenHub.ProductManager.Api.Mappers;

[Mapper]
public partial class ProductMapper
{
    // Accessor → Manager
    [MapperIgnoreSource("SysPeriod")]
    [MapperIgnoreTarget("ManufacturerName")]
    [MapperIgnoreTarget("BrandName")]
    [MapperIgnoreTarget("ConditionTypeName")]
    [MapperIgnoreTarget("AvailabilityStatusName")]
    [MapperIgnoreTarget("WeightUomCode")]
    [MapperIgnoreTarget("Prices")]
    public partial ProductDto ToDto(ProductEntity entity);

    public partial IReadOnlyList<ProductDto> ToDtoList(IEnumerable<ProductEntity> entities);

    [MapperIgnoreSource("TenantId")]
    [MapperIgnoreSource("CreatedAt")]
    [MapperIgnoreSource("DeletedAt")]
    [MapperIgnoreTarget("PriceTypeName")]
    [MapperIgnoreTarget("CurrencyCode")]
    public partial ProductPriceDto ToDto(ProductPriceEntity entity);

    public partial IReadOnlyList<ProductPriceDto> ToDtoPriceList(IEnumerable<ProductPriceEntity> entities);

    // Manager → Accessor
    [MapperIgnoreSource("ManufacturerName")]
    [MapperIgnoreSource("BrandName")]
    [MapperIgnoreSource("ConditionTypeName")]
    [MapperIgnoreSource("AvailabilityStatusName")]
    [MapperIgnoreSource("WeightUomCode")]
    [MapperIgnoreSource("Prices")]
    [MapperIgnoreTarget("SysPeriod")]
    public partial ProductEntity ToEntity(ProductDto dto);

    [MapperIgnoreSource("PriceTypeName")]
    [MapperIgnoreSource("CurrencyCode")]
    [MapperIgnoreTarget("TenantId")]
    [MapperIgnoreTarget("CreatedAt")]
    [MapperIgnoreTarget("DeletedAt")]
    public partial ProductPriceEntity ToEntity(ProductPriceDto dto);

    // Manager → Controller
    [MapperIgnoreSource("TenantId")]
    [MapperIgnoreSource("ManufacturerId")]
    [MapperIgnoreSource("BrandId")]
    [MapperIgnoreSource("ConditionTypeId")]
    [MapperIgnoreSource("AvailabilityStatusId")]
    [MapperIgnoreSource("WeightUomId")]
    [MapperIgnoreSource("BaseUomId")]
    [MapperIgnoreSource("CreateUserId")]
    [MapperIgnoreSource("ModifyUserId")]
    [MapperIgnoreSource("DeleteUserId")]
    [MapperIgnoreSource("DeletedAt")]
    public partial ProductResponse ToResponse(ProductDto dto);

    public partial IReadOnlyList<ProductResponse> ToResponseList(IEnumerable<ProductDto> dtos);

    [MapperIgnoreSource("ProductId")]
    [MapperIgnoreSource("PriceTypeId")]
    [MapperIgnoreSource("CurrencyId")]
    [MapperIgnoreSource("IsActive")]
    public partial ProductPriceResponse ToResponse(ProductPriceDto dto);

    // Controller → Manager
    [MapperIgnoreTarget("Id")]
    [MapperIgnoreTarget("TenantId")]
    [MapperIgnoreTarget("ManufacturerName")]
    [MapperIgnoreTarget("BrandName")]
    [MapperIgnoreTarget("ConditionTypeName")]
    [MapperIgnoreTarget("AvailabilityStatusName")]
    [MapperIgnoreTarget("WeightUomCode")]
    [MapperIgnoreTarget("IsActive")]
    [MapperIgnoreTarget("IsPublished")]
    [MapperIgnoreTarget("CreateUserId")]
    [MapperIgnoreTarget("ModifyUserId")]
    [MapperIgnoreTarget("DeleteUserId")]
    [MapperIgnoreTarget("CreatedAt")]
    [MapperIgnoreTarget("UpdatedAt")]
    [MapperIgnoreTarget("DeletedAt")]
    [MapperIgnoreTarget("Prices")]
    public partial ProductDto ToDto(CreateProductRequest request);
}
