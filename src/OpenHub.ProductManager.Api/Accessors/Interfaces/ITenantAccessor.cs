using OpenHub.ProductManager.Api.Models.Dtos;

namespace OpenHub.ProductManager.Api.Accessors.Interfaces;

public interface ITenantAccessor
{
    Task<TenantDto?> GetBySlugAsync(string slug, CancellationToken ct = default);
}
