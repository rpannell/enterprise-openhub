using Microsoft.AspNetCore.Http;
using OpenHub.SalesOrg.Api.Accessors.Interfaces;
using OpenHub.SalesOrg.Api.Accessors.Models;
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
    // A role with no parent is the top of its chain. Each step down a parent link drops the
    // derived level by this much — matches the spacing already used by hand-picked levels
    // (700/800/900/1000), so existing data lines up if it's later given parents.
    private const int RootLevel = 1000;
    private const int LevelStep = 100;

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
        var level = RootLevel;
        if (request.ParentRoleId.HasValue)
        {
            var parent = await accessor.GetByIdAsync(request.ParentRoleId.Value, ct)
                ?? throw new KeyNotFoundException($"Parent role {request.ParentRoleId} not found.");
            level = (parent.HierarchyLevel ?? RootLevel) - LevelStep;
        }

        var dto = mapper.ToDto(request);
        dto.TenantId = TenantId;
        dto.ActiveStatus = true;
        dto.HierarchyLevel = level;

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

        if (request.ParentRoleId.HasValue)
        {
            var newParentId = request.ParentRoleId.Value;
            if (newParentId == id)
                throw new InvalidOperationException("A sales role cannot be its own parent.");
            var parent = await accessor.GetByIdAsync(newParentId, ct)
                ?? throw new KeyNotFoundException($"Parent role {newParentId} not found.");

            // Reorg cycle guard: the new parent must not be a descendant of the role being moved.
            var allRoles = await accessor.GetAllAsync(ct);
            var descendantIds = GetDescendantRoleIds(allRoles, id);
            if (descendantIds.Contains(newParentId))
                throw new InvalidOperationException("Cannot reparent a sales role under one of its own descendants.");

            existing.ParentRoleId = newParentId;
            existing.HierarchyLevel = (parent.HierarchyLevel ?? RootLevel) - LevelStep;
        }
        if (request.Name is not null) existing.Name = request.Name;
        if (request.Description is not null) existing.Description = request.Description;
        if (request.ActiveStatus.HasValue) existing.ActiveStatus = request.ActiveStatus.Value;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        var updated = await accessor.UpdateAsync(existing, ct);

        // This role's own derived level just changed — cascade that down to every role beneath
        // it in the chain so the whole subtree stays consistent with its new position.
        if (request.ParentRoleId.HasValue)
        {
            var allRolesAfter = await accessor.GetAllAsync(ct);
            await RecomputeDescendantLevelsAsync(allRolesAfter, updated, ct);
        }

        return mapper.ToDto(updated);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await accessor.GetByIdAsync(id, ct);
        if (existing is null) return false;

        await accessor.SoftDeleteAsync(id, ct);
        return true;
    }

    private async Task RecomputeDescendantLevelsAsync(
        IReadOnlyList<SalesRoleEntity> allRoles, SalesRoleEntity root, CancellationToken ct)
    {
        var childrenByParent = allRoles
            .Where(r => r.ParentRoleId.HasValue)
            .GroupBy(r => r.ParentRoleId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var queue = new Queue<SalesRoleEntity>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current.Id, out var children)) continue;

            foreach (var child in children)
            {
                child.HierarchyLevel = (current.HierarchyLevel ?? RootLevel) - LevelStep;
                child.UpdatedAt = DateTimeOffset.UtcNow;
                await accessor.UpdateAsync(child, ct);
                queue.Enqueue(child);
            }
        }
    }

    private static IReadOnlyList<Guid> GetDescendantRoleIds(IReadOnlyList<SalesRoleEntity> allRoles, Guid roleId)
    {
        var childrenByParent = allRoles
            .Where(r => r.ParentRoleId.HasValue)
            .GroupBy(r => r.ParentRoleId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Id).ToList());

        var result = new List<Guid>();
        var visited = new HashSet<Guid> { roleId };
        var queue = new Queue<Guid>();
        queue.Enqueue(roleId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenByParent.TryGetValue(current, out var children)) continue;
            foreach (var childId in children)
            {
                if (!visited.Add(childId)) continue;
                result.Add(childId);
                queue.Enqueue(childId);
            }
        }
        return result;
    }
}
