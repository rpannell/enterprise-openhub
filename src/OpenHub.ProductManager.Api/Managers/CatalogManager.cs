using Microsoft.AspNetCore.Http;
using OpenHub.ProductManager.Api.Accessors.Interfaces;
using OpenHub.ProductManager.Api.Managers.Interfaces;
using OpenHub.ProductManager.Api.Managers.Models;
using OpenHub.ProductManager.Api.Mappers;
using OpenHub.ProductManager.Api.Models.Requests;

namespace OpenHub.ProductManager.Api.Managers;

public class CatalogManager(
    ICatalogLookupAccessor accessor,
    LookupMapper mapper,
    IHttpContextAccessor http) : ICatalogManager
{
    private Guid TenantId =>
        http.HttpContext?.Items["TenantId"] as Guid?
            ?? throw new InvalidOperationException("Tenant context not set. Ensure TenantMiddleware is registered.");

    public async Task<LookupItemDto> CreateManufacturerAsync(CreateManufacturerRequest request, CancellationToken ct = default)
    {
        var entity = await accessor.InsertManufacturerAsync(request.Name, TenantId, ct);
        return mapper.ToDto(entity);
    }

    public async Task<LookupItemDto> CreateBrandAsync(CreateBrandRequest request, CancellationToken ct = default)
    {
        var entity = await accessor.InsertBrandAsync(request.Name, TenantId, ct);
        return mapper.ToLookupDto(entity);
    }
}
