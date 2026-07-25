using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using OpenHub.Accessor.RabbitMq;
using OpenHub.SalesOrg.Api.Accessors.Interfaces;
using OpenHub.SalesOrg.Api.Managers.Interfaces;
using OpenHub.SalesOrg.Api.Managers.Models;
using OpenHub.SalesOrg.Api.Managers.Options;
using OpenHub.SalesOrg.Api.Mappers;
using OpenHub.SalesOrg.Api.Models.Requests;
using OpenHub.SalesOrg.Contracts;

namespace OpenHub.SalesOrg.Api.Managers;

public class SalesAssignmentManager(
    ISalesAssignmentAccessor accessor,
    ISalesPersonAccessor personAccessor,
    ISalesOrganizationUnitAccessor orgUnitAccessor,
    ISalesRoleAccessor roleAccessor,
    SalesAssignmentMapper mapper,
    IHttpContextAccessor http,
    IMessagePublisher messagePublisher,
    IOptions<SalesAssignmentEventOptions> salesAssignmentEventOptions) : ISalesAssignmentManager
{
    private Guid TenantId =>
        http.HttpContext?.Items["TenantId"] as Guid?
            ?? throw new InvalidOperationException("Tenant context not set. Ensure TenantMiddleware is registered.");

    public async Task<IReadOnlyList<SalesAssignmentDto>> GetAllAsync(CancellationToken ct = default)
        => await HydrateAllAsync(mapper.ToDtoList(await accessor.GetAllAsync(ct)), ct);

    public async Task<SalesAssignmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await accessor.GetByIdAsync(id, ct);
        if (entity is null) return null;
        return await HydrateAsync(mapper.ToDto(entity), ct);
    }

    public async Task<SalesAssignmentDto> CreateAsync(CreateSalesAssignmentRequest request, Guid createUserId, CancellationToken ct = default)
    {
        if (await personAccessor.GetByIdAsync(request.SalesPersonId, ct) is null)
            throw new KeyNotFoundException($"Sales person {request.SalesPersonId} not found.");
        if (await orgUnitAccessor.GetByIdAsync(request.OrganizationUnitId, ct) is null)
            throw new KeyNotFoundException($"Organization unit {request.OrganizationUnitId} not found.");
        if (await roleAccessor.GetByIdAsync(request.RoleId, ct) is null)
            throw new KeyNotFoundException($"Sales role {request.RoleId} not found.");

        var dto = mapper.ToDto(request);
        dto.TenantId = TenantId;
        dto.ActiveStatus = true;
        dto.CreateUserId = createUserId;
        dto.ModifyUserId = createUserId;

        var entity = mapper.ToEntity(dto);
        entity.Id = Guid.NewGuid();
        entity.CreatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        // Closing out any prior active+primary assignment for this person happens inside the
        // accessor's transaction (see SalesAssignmentAccessor.CreateAsync) — history is preserved
        // by never overwriting a row in place.
        var created = await accessor.CreateAsync(entity, ct);

        await messagePublisher.PublishEntityEventAsync(salesAssignmentEventOptions.Value, new SalesAssignmentChangedEvent
        {
            SalesAssignmentId = created.Id,
            TenantId = TenantId,
            SalesPersonId = created.SalesPersonId,
            OrganizationUnitId = created.OrganizationUnitId,
        }, ct);

        return await HydrateAsync(mapper.ToDto(created), ct);
    }

    public async Task<SalesAssignmentDto?> UpdateAsync(Guid id, UpdateSalesAssignmentRequest request, Guid modifyUserId, CancellationToken ct = default)
    {
        var existing = await accessor.GetByIdAsync(id, ct);
        if (existing is null) return null;

        if (request.EffectiveEndDate.HasValue) existing.EffectiveEndDate = request.EffectiveEndDate;
        if (request.PrimaryAssignmentFlag.HasValue) existing.PrimaryAssignmentFlag = request.PrimaryAssignmentFlag.Value;
        if (request.ActiveStatus.HasValue) existing.ActiveStatus = request.ActiveStatus.Value;
        existing.ModifyUserId = modifyUserId;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        var updated = await accessor.UpdateAsync(existing, ct);

        await messagePublisher.PublishEntityEventAsync(salesAssignmentEventOptions.Value, new SalesAssignmentChangedEvent
        {
            SalesAssignmentId = updated.Id,
            TenantId = TenantId,
            SalesPersonId = updated.SalesPersonId,
            OrganizationUnitId = updated.OrganizationUnitId,
        }, ct);

        return await HydrateAsync(mapper.ToDto(updated), ct);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid deleteUserId, CancellationToken ct = default)
    {
        var existing = await accessor.GetByIdAsync(id, ct);
        if (existing is null) return false;

        await accessor.SoftDeleteAsync(id, deleteUserId, ct);

        await messagePublisher.PublishEntityEventAsync(salesAssignmentEventOptions.Value, new SalesAssignmentChangedEvent
        {
            SalesAssignmentId = id,
            TenantId = TenantId,
            SalesPersonId = existing.SalesPersonId,
            OrganizationUnitId = existing.OrganizationUnitId,
            IsDeleted = true,
        }, ct);

        return true;
    }

    private async Task<SalesAssignmentDto> HydrateAsync(SalesAssignmentDto dto, CancellationToken ct)
    {
        var person = await personAccessor.GetByIdAsync(dto.SalesPersonId, ct);
        dto.SalesPersonName = person is null ? null : $"{person.FirstName} {person.LastName}".Trim();

        var unit = await orgUnitAccessor.GetByIdAsync(dto.OrganizationUnitId, ct);
        dto.OrganizationUnitName = unit?.Name;

        var role = await roleAccessor.GetByIdAsync(dto.RoleId, ct);
        dto.RoleName = role?.Name;

        return dto;
    }

    private async Task<IReadOnlyList<SalesAssignmentDto>> HydrateAllAsync(IReadOnlyList<SalesAssignmentDto> dtos, CancellationToken ct)
    {
        var result = new List<SalesAssignmentDto>(dtos.Count);
        foreach (var dto in dtos)
            result.Add(await HydrateAsync(dto, ct));
        return result;
    }
}
