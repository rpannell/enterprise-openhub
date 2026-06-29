using OpenHub.ProductManager.Api.Models.Dtos;
using OpenHub.ProductManager.Api.Models.Entities;
using OpenHub.ProductManager.Api.Models.Requests;
using OpenHub.ProductManager.Api.Models.Responses;
using Riok.Mapperly.Abstractions;

namespace OpenHub.ProductManager.Api.Mappers;

[Mapper]
public partial class ProductMapper
{
    // Accessor → Manager
    public partial ProductDto ToDto(ProductEntity entity);
    public partial IReadOnlyList<ProductDto> ToDtoList(IEnumerable<ProductEntity> entities);
    public partial ProductPriceDto ToDto(ProductPriceEntity entity);
    public partial IReadOnlyList<ProductPriceDto> ToDtoPriceList(IEnumerable<ProductPriceEntity> entities);

    // Manager → Accessor
    public partial ProductEntity ToEntity(ProductDto dto);
    public partial ProductPriceEntity ToEntity(ProductPriceDto dto);

    // Manager/Engine → Controller
    public partial ProductResponse ToResponse(ProductDto dto);
    public partial IReadOnlyList<ProductResponse> ToResponseList(IEnumerable<ProductDto> dtos);
    public partial ProductPriceResponse ToResponse(ProductPriceDto dto);

    // Controller → Manager
    public partial ProductDto ToDto(CreateProductRequest request);
}
