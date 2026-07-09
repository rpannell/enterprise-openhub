using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OpenHub.SalesOrg.Api.Accessors.Data;
using OpenHub.SalesOrg.Api.Accessors.Interfaces;
using OpenHub.SalesOrg.Api.Accessors.Models;

namespace OpenHub.SalesOrg.Api.Accessors;

public class SalesPersonAccessor(SalesOrgDbContext db, IHttpContextAccessor http) : ISalesPersonAccessor
{
    private Guid TenantId =>
        http.HttpContext?.Items["TenantId"] as Guid?
            ?? throw new InvalidOperationException("Tenant context not set. Ensure TenantMiddleware is registered.");

    private async Task SetTenantContextAsync(Guid tenantId, CancellationToken ct)
        => await db.Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.current_tenant_id', {0}, true)", [tenantId.ToString()], ct);

    public async Task<SalesPersonEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            var result = await db.SalesPeople.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);
            await tx.CommitAsync(ct);
            return result;
        });
    }

    public async Task<IReadOnlyList<SalesPersonEntity>> GetAllAsync(CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            var results = (IReadOnlyList<SalesPersonEntity>)await db.SalesPeople.AsNoTracking()
                .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
                .ToListAsync(ct);
            await tx.CommitAsync(ct);
            return results;
        });
    }

    public async Task<SalesPersonEntity> CreateAsync(SalesPersonEntity entity, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            db.SalesPeople.Add(entity);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return entity;
    }

    public async Task<SalesPersonEntity> UpdateAsync(SalesPersonEntity entity, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            db.SalesPeople.Update(entity);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return entity;
    }

    public async Task SoftDeleteAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            var entity = await db.SalesPeople.FirstOrDefaultAsync(s => s.Id == id, ct)
                ?? throw new KeyNotFoundException($"Sales person {id} not found.");
            entity.DeletedAt = DateTimeOffset.UtcNow;
            entity.ActiveStatus = false;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }
}
