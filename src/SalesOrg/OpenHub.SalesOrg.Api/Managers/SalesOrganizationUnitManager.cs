using Microsoft.AspNetCore.Http;
using OpenHub.SalesOrg.Api.Accessors.Interfaces;
using OpenHub.SalesOrg.Api.Managers.Engines.Interfaces;
using OpenHub.SalesOrg.Api.Managers.Interfaces;
using OpenHub.SalesOrg.Api.Managers.Models;
using OpenHub.SalesOrg.Api.Mappers;
using OpenHub.SalesOrg.Api.Models.Requests;

namespace OpenHub.SalesOrg.Api.Managers;

public class SalesOrganizationUnitManager(
    ISalesOrganizationUnitAccessor accessor,
    ISalesOrgHierarchyEngine hierarchyEngine,
    SalesOrganizationUnitMapper mapper,
    IHttpContextAccessor http) : ISalesOrganizationUnitManager
{
    private Guid TenantId =>
        http.HttpContext?.Items["TenantId"] as Guid?
            ?? throw new InvalidOperationException("Tenant context not set. Ensure TenantMiddleware is registered.");

    public async Task<IReadOnlyList<SalesOrganizationUnitDto>> GetAllAsync(CancellationToken ct = default)
        => mapper.ToDtoList(await accessor.GetAllAsync(ct));

    public async Task<SalesOrganizationUnitDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await accessor.GetByIdAsync(id, ct);
        return entity is null ? null : mapper.ToDto(entity);
    }

    public async Task<SalesOrganizationUnitDto> CreateAsync(CreateSalesOrganizationUnitRequest request, Guid createUserId, CancellationToken ct = default)
    {
        if (request.ParentOrganizationUnitId.HasValue
            && await accessor.GetByIdAsync(request.ParentOrganizationUnitId.Value, ct) is null)
            throw new KeyNotFoundException($"Parent organization unit {request.ParentOrganizationUnitId} not found.");

        var dto = mapper.ToDto(request);
        dto.TenantId = TenantId;
        dto.ActiveStatus = true;
        dto.CreateUserId = createUserId;
        dto.ModifyUserId = createUserId;

        var entity = mapper.ToEntity(dto);
        entity.Id = Guid.NewGuid();
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        var created = await accessor.CreateAsync(entity, ct);
        return mapper.ToDto(created);
    }

    public async Task<SalesOrganizationUnitDto?> UpdateAsync(Guid id, UpdateSalesOrganizationUnitRequest request, Guid modifyUserId, CancellationToken ct = default)
    {
        var existing = await accessor.GetByIdAsync(id, ct);
        if (existing is null) return null;

        if (request.ParentOrganizationUnitId.HasValue)
        {
            var newParentId = request.ParentOrganizationUnitId.Value;
            if (newParentId == id)
                throw new InvalidOperationException("An organization unit cannot be its own parent.");
            if (await accessor.GetByIdAsync(newParentId, ct) is null)
                throw new KeyNotFoundException($"Parent organization unit {newParentId} not found.");

            // Reorg cycle guard: the new parent must not be a descendant of the unit being moved.
            var allUnits = mapper.ToDtoList(await accessor.GetAllAsync(ct));
            var descendantIds = hierarchyEngine.GetDescendantOrgUnitIds(allUnits, id);
            if (descendantIds.Contains(newParentId))
                throw new InvalidOperationException(
                    "Cannot reparent an organization unit under one of its own descendants.");

            existing.ParentOrganizationUnitId = newParentId;
        }
        if (request.Name is not null) existing.Name = request.Name;
        if (request.Description is not null) existing.Description = request.Description;
        if (request.OrganizationUnitType is not null) existing.OrganizationUnitType = request.OrganizationUnitType;
        if (request.ActiveStatus.HasValue) existing.ActiveStatus = request.ActiveStatus.Value;
        existing.ModifyUserId = modifyUserId;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        var updated = await accessor.UpdateAsync(existing, ct);
        return mapper.ToDto(updated);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid deleteUserId, CancellationToken ct = default)
    {
        var existing = await accessor.GetByIdAsync(id, ct);
        if (existing is null) return false;

        await accessor.SoftDeleteAsync(id, deleteUserId, ct);
        return true;
    }

    public async Task<IReadOnlyList<Guid>> GetDescendantsAsync(Guid unitId, CancellationToken ct = default)
    {
        var allUnits = mapper.ToDtoList(await accessor.GetAllAsync(ct));
        return hierarchyEngine.GetDescendantOrgUnitIds(allUnits, unitId);
    }

    public async Task<IReadOnlyList<Guid>> GetAncestorsAsync(Guid unitId, CancellationToken ct = default)
    {
        var allUnits = mapper.ToDtoList(await accessor.GetAllAsync(ct));
        return hierarchyEngine.GetAncestorOrgUnitIds(allUnits, unitId);
    }
}
