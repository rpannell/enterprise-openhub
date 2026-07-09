using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OpenHub.SalesOrg.Api.Accessors.Data;
using OpenHub.SalesOrg.Api.Accessors.Interfaces;
using OpenHub.SalesOrg.Api.Accessors.Models;

namespace OpenHub.SalesOrg.Api.Accessors;

public class SalesOrganizationUnitAccessor(SalesOrgDbContext db, IHttpContextAccessor http) : ISalesOrganizationUnitAccessor
{
    private Guid TenantId =>
        http.HttpContext?.Items["TenantId"] as Guid?
            ?? throw new InvalidOperationException("Tenant context not set. Ensure TenantMiddleware is registered.");

    private async Task SetTenantContextAsync(Guid tenantId, CancellationToken ct)
        => await db.Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.current_tenant_id', {0}, true)", [tenantId.ToString()], ct);

    public async Task<SalesOrganizationUnitEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            var result = await db.SalesOrganizationUnits.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
            await tx.CommitAsync(ct);
            return result;
        });
    }

    public async Task<IReadOnlyList<SalesOrganizationUnitEntity>> GetAllAsync(CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            var results = (IReadOnlyList<SalesOrganizationUnitEntity>)await db.SalesOrganizationUnits.AsNoTracking()
                .OrderBy(u => u.Name)
                .ToListAsync(ct);
            await tx.CommitAsync(ct);
            return results;
        });
    }

    public async Task<SalesOrganizationUnitEntity> CreateAsync(SalesOrganizationUnitEntity entity, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            db.SalesOrganizationUnits.Add(entity);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return entity;
    }

    public async Task<SalesOrganizationUnitEntity> UpdateAsync(SalesOrganizationUnitEntity entity, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            db.SalesOrganizationUnits.Update(entity);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return entity;
    }

    public async Task SoftDeleteAsync(Guid id, Guid deleteUserId, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            var entity = await db.SalesOrganizationUnits.FirstOrDefaultAsync(u => u.Id == id, ct)
                ?? throw new KeyNotFoundException($"Sales organization unit {id} not found.");
            entity.DeletedAt = DateTimeOffset.UtcNow;
            entity.ActiveStatus = false;
            entity.DeleteUserId = deleteUserId;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }
}
