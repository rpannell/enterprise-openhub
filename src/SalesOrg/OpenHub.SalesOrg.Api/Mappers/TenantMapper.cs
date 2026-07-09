using OpenHub.SalesOrg.Api.Accessors.Models;
using OpenHub.SalesOrg.Api.Managers.Models;
using Riok.Mapperly.Abstractions;

namespace OpenHub.SalesOrg.Api.Mappers;

[Mapper]
public partial class TenantMapper
{
    [MapperIgnoreSource(nameof(TenantEntity.CreatedAt))]
    [MapperIgnoreSource(nameof(TenantEntity.UpdatedAt))]
    [MapperIgnoreSource(nameof(TenantEntity.DeletedAt))]
    public partial TenantDto ToDto(TenantEntity entity);
}
