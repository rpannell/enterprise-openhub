using Microsoft.AspNetCore.Http;
using OpenHub.SalesOrg.Api.Accessors.Interfaces;
using OpenHub.SalesOrg.Api.Managers.Interfaces;
using OpenHub.SalesOrg.Api.Managers.Models;
using OpenHub.SalesOrg.Api.Mappers;
using OpenHub.SalesOrg.Api.Models.Requests;

namespace OpenHub.SalesOrg.Api.Managers;

public class SalesRoleManager(
    ISalesRoleAccessor accessor,
    SalesRoleMapper mapper,
    IHttpContextAccessor http) : ISalesRoleManager
{
    private Guid TenantId =>
        http.HttpContext?.Items["TenantId"] as Guid?
            ?? throw new InvalidOperationException("Tenant context not set. Ensure TenantMiddleware is registered.");

    public async Task<IReadOnlyList<SalesRoleDto>> GetAllAsync(CancellationToken ct = default)
        => mapper.ToDtoList(await accessor.GetAllAsync(ct));

    public async Task<SalesRoleDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await accessor.GetByIdAsync(id, ct);
        return entity is null ? null : mapper.ToDto(entity);
    }

    public async Task<SalesRoleDto> CreateAsync(CreateSalesRoleRequest request, CancellationToken ct = default)
    {
        var dto = mapper.ToDto(request);
        dto.TenantId = TenantId;
        dto.ActiveStatus = true;

        var entity = mapper.ToEntity(dto);
        entity.Id = Guid.NewGuid();
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        var created = await accessor.CreateAsync(entity, ct);
        return mapper.ToDto(created);
    }

    public async Task<SalesRoleDto?> UpdateAsync(Guid id, UpdateSalesRoleRequest request, CancellationToken ct = default)
    {
        var existing = await accessor.GetByIdAsync(id, ct);
        if (existing is null) return null;

        if (request.Name is not null) existing.Name = request.Name;
        if (request.Description is not null) existing.Description = request.Description;
        if (request.HierarchyLevel.HasValue) existing.HierarchyLevel = request.HierarchyLevel;
        if (request.ActiveStatus.HasValue) existing.ActiveStatus = request.ActiveStatus.Value;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        var updated = await accessor.UpdateAsync(existing, ct);
        return mapper.ToDto(updated);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await accessor.GetByIdAsync(id, ct);
        if (existing is null) return false;

        await accessor.SoftDeleteAsync(id, ct);
        return true;
    }
}
