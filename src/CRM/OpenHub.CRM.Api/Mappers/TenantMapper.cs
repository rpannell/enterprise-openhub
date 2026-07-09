using OpenHub.CRM.Api.Accessors.Models;
using OpenHub.CRM.Api.Managers.Models;
using Riok.Mapperly.Abstractions;

namespace OpenHub.CRM.Api.Mappers;

[Mapper]
public partial class TenantMapper
{
    [MapperIgnoreSource(nameof(TenantEntity.CreatedAt))]
    [MapperIgnoreSource(nameof(TenantEntity.UpdatedAt))]
    [MapperIgnoreSource(nameof(TenantEntity.DeletedAt))]
    public partial TenantDto ToDto(TenantEntity entity);
}
