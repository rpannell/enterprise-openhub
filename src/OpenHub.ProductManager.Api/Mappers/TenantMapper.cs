using OpenHub.ProductManager.Api.Models.Dtos;
using OpenHub.ProductManager.Api.Models.Entities;
using Riok.Mapperly.Abstractions;

namespace OpenHub.ProductManager.Api.Mappers;

[Mapper]
public partial class TenantMapper
{
    public partial TenantDto ToDto(TenantEntity entity);
}
