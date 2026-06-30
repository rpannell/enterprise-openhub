using OpenHub.ProductManager.Api.Accessors.Models;
using OpenHub.ProductManager.Api.Managers.Models;
using Riok.Mapperly.Abstractions;

namespace OpenHub.ProductManager.Api.Mappers;

[Mapper]
public partial class TenantMapper
{
    [MapperIgnoreSource("CreatedAt")]
    [MapperIgnoreSource("UpdatedAt")]
    [MapperIgnoreSource("DeletedAt")]
    public partial TenantDto ToDto(TenantEntity entity);
}
