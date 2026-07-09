using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OpenHub.SalesOrg.Api.Accessors.Data;
using OpenHub.SalesOrg.Api.Accessors.Interfaces;
using OpenHub.SalesOrg.Api.Accessors.Models;

namespace OpenHub.SalesOrg.Api.Accessors;

public class SalesAssignmentAccessor(SalesOrgDbContext db, IHttpContextAccessor http) : ISalesAssignmentAccessor
{
    private Guid TenantId =>
        http.HttpContext?.Items["TenantId"] as Guid?
            ?? throw new InvalidOperationException("Tenant context not set. Ensure TenantMiddleware is registered.");

    private async Task SetTenantContextAsync(Guid tenantId, CancellationToken ct)
        => await db.Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.current_tenant_id', {0}, true)", [tenantId.ToString()], ct);

    public async Task<SalesAssignmentEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            var result = await db.SalesAssignments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct);
            await tx.CommitAsync(ct);
            return result;
        });
    }

    public async Task<IReadOnlyList<SalesAssignmentEntity>> GetAllAsync(CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            var results = (IReadOnlyList<SalesAssignmentEntity>)await db.SalesAssignments.AsNoTracking()
                .OrderByDescending(a => a.EffectiveStartDate)
                .ToListAsync(ct);
            await tx.CommitAsync(ct);
            return results;
        });
    }

    public async Task<SalesAssignmentEntity> CreateAsync(SalesAssignmentEntity entity, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);

            // If this is a new active+primary assignment, close out any existing active+primary
            // assignment for the same person first — history is preserved by never overwriting a
            // row in place; moving a person means closing the old assignment and creating a new one.
            if (entity.ActiveStatus && entity.PrimaryAssignmentFlag)
            {
                var existingPrimary = await db.SalesAssignments
                    .Where(a => a.SalesPersonId == entity.SalesPersonId
                        && a.ActiveStatus && a.PrimaryAssignmentFlag && a.Id != entity.Id)
                    .ToListAsync(ct);

                foreach (var previous in existingPrimary)
                {
                    previous.ActiveStatus = false;
                    previous.EffectiveEndDate ??= entity.EffectiveStartDate;
                    previous.UpdatedAt = DateTimeOffset.UtcNow;
                }
            }

            db.SalesAssignments.Add(entity);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return entity;
    }

    public async Task<SalesAssignmentEntity> UpdateAsync(SalesAssignmentEntity entity, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            db.SalesAssignments.Update(entity);
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
            var entity = await db.SalesAssignments.FirstOrDefaultAsync(a => a.Id == id, ct)
                ?? throw new KeyNotFoundException($"Sales assignment {id} not found.");
            entity.DeletedAt = DateTimeOffset.UtcNow;
            entity.ActiveStatus = false;
            entity.DeleteUserId = deleteUserId;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }
}
