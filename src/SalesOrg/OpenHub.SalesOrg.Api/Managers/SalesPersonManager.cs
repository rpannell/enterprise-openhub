using Microsoft.AspNetCore.Http;
using OpenHub.SalesOrg.Api.Accessors.Interfaces;
using OpenHub.SalesOrg.Api.Managers.Engines.Interfaces;
using OpenHub.SalesOrg.Api.Managers.Interfaces;
using OpenHub.SalesOrg.Api.Managers.Models;
using OpenHub.SalesOrg.Api.Mappers;
using OpenHub.SalesOrg.Api.Models.Requests;

namespace OpenHub.SalesOrg.Api.Managers;

public class SalesPersonManager(
    ISalesPersonAccessor personAccessor,
    ISalesAssignmentAccessor assignmentAccessor,
    ISalesRoleAccessor roleAccessor,
    ISalesOrganizationUnitAccessor orgUnitAccessor,
    ISalesOrgHierarchyEngine hierarchyEngine,
    SalesPersonMapper personMapper,
    SalesAssignmentMapper assignmentMapper,
    SalesRoleMapper roleMapper,
    SalesOrganizationUnitMapper orgUnitMapper,
    IHttpContextAccessor http) : ISalesPersonManager
{
    private Guid TenantId =>
        http.HttpContext?.Items["TenantId"] as Guid?
            ?? throw new InvalidOperationException("Tenant context not set. Ensure TenantMiddleware is registered.");

    public async Task<IReadOnlyList<SalesPersonDto>> GetAllAsync(CancellationToken ct = default)
        => personMapper.ToDtoList(await personAccessor.GetAllAsync(ct));

    public async Task<SalesPersonDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await personAccessor.GetByIdAsync(id, ct);
        return entity is null ? null : personMapper.ToDto(entity);
    }

    public async Task<SalesPersonDto> CreateAsync(CreateSalesPersonRequest request, Guid createUserId, CancellationToken ct = default)
    {
        var dto = personMapper.ToDto(request);
        dto.TenantId = TenantId;
        dto.ActiveStatus = true;

        var entity = personMapper.ToEntity(dto);
        entity.Id = Guid.NewGuid();
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        var created = await personAccessor.CreateAsync(entity, ct);
        return personMapper.ToDto(created);
    }

    public async Task<SalesPersonDto?> UpdateAsync(Guid id, UpdateSalesPersonRequest request, Guid modifyUserId, CancellationToken ct = default)
    {
        var existing = await personAccessor.GetByIdAsync(id, ct);
        if (existing is null) return null;

        if (request.FirstName is not null) existing.FirstName = request.FirstName;
        if (request.LastName is not null) existing.LastName = request.LastName;
        if (request.Email is not null) existing.Email = request.Email;
        if (request.Phone is not null) existing.Phone = request.Phone;
        if (request.JobTitle is not null) existing.JobTitle = request.JobTitle;
        if (request.EmployeeNumber is not null) existing.EmployeeNumber = request.EmployeeNumber;
        if (request.ActiveStatus.HasValue) existing.ActiveStatus = request.ActiveStatus.Value;
        if (request.StartDate.HasValue) existing.StartDate = request.StartDate;
        if (request.EndDate.HasValue) existing.EndDate = request.EndDate;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        var updated = await personAccessor.UpdateAsync(existing, ct);
        return personMapper.ToDto(updated);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var existing = await personAccessor.GetByIdAsync(id, ct);
        if (existing is null) return false;

        await personAccessor.SoftDeleteAsync(id, ct);
        return true;
    }

    public async Task<IReadOnlyList<SalesPersonDto>> GetManagerChainAsync(Guid salesPersonId, CancellationToken ct = default)
    {
        var (assignments, roles, units) = await LoadHierarchyDataAsync(ct);
        var chainIds = hierarchyEngine.GetManagerChain(assignments, roles, units, salesPersonId);
        return await ResolvePeopleAsync(chainIds, ct);
    }

    public async Task<IReadOnlyList<SalesPersonDto>> GetReportsAsync(Guid salesPersonId, CancellationToken ct = default)
    {
        var (assignments, roles, units) = await LoadHierarchyDataAsync(ct);
        var reportIds = hierarchyEngine.GetReports(assignments, roles, units, salesPersonId);
        return await ResolvePeopleAsync(reportIds, ct);
    }

    private async Task<(IReadOnlyList<SalesAssignmentDto>, IReadOnlyList<SalesRoleDto>, IReadOnlyList<SalesOrganizationUnitDto>)> LoadHierarchyDataAsync(CancellationToken ct)
    {
        var assignments = assignmentMapper.ToDtoList(await assignmentAccessor.GetAllAsync(ct));
        var roles = roleMapper.ToDtoList(await roleAccessor.GetAllAsync(ct));
        var units = orgUnitMapper.ToDtoList(await orgUnitAccessor.GetAllAsync(ct));
        return (assignments, roles, units);
    }

    private async Task<IReadOnlyList<SalesPersonDto>> ResolvePeopleAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        var result = new List<SalesPersonDto>();
        foreach (var id in ids)
        {
            var entity = await personAccessor.GetByIdAsync(id, ct);
            if (entity is not null)
                result.Add(personMapper.ToDto(entity));
        }
        return result;
    }
}
