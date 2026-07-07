using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OpenHub.ProductManager.Api.Accessors.Data;
using OpenHub.ProductManager.Api.Accessors.Interfaces;
using OpenHub.ProductManager.Api.Accessors.Models;

namespace OpenHub.ProductManager.Api.Accessors;

public class ProductAccessor(CatalogDbContext db, IHttpContextAccessor http) : IProductAccessor
{
    private Guid TenantId =>
        http.HttpContext?.Items["TenantId"] as Guid?
            ?? throw new InvalidOperationException("Tenant context not set. Ensure TenantMiddleware is registered.");

    private async Task SetTenantContextAsync(Guid tenantId, CancellationToken ct)
        => await db.Database.ExecuteSqlRawAsync(
            "SELECT set_config('app.current_tenant_id', {0}, true)", [tenantId.ToString()], ct);

    public async Task<ProductEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            var result = await db.Products.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == id, ct);
            await tx.CommitAsync(ct);
            return result;
        });
    }

    public async Task<IReadOnlyList<ProductEntity>> GetAllAsync(CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            var results = (IReadOnlyList<ProductEntity>)await db.Products.AsNoTracking()
                .OrderBy(p => p.Name)
                .ToListAsync(ct);
            await tx.CommitAsync(ct);
            return results;
        });
    }

    public async Task<(IReadOnlyList<ProductEntity> Items, int TotalCount)> GetPagedAsync(
        int page, int pageSize, string? search, Guid? manufacturerId, Guid? brandId,
        Guid? conditionTypeId, Guid? availabilityStatusId, bool? isActive, bool? isPublished,
        CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);

            var query = db.Products.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(p => EF.Functions.ILike(p.Sku, $"%{search}%") || EF.Functions.ILike(p.Name, $"%{search}%"));
            if (manufacturerId.HasValue) query = query.Where(p => p.ManufacturerId == manufacturerId);
            if (brandId.HasValue) query = query.Where(p => p.BrandId == brandId);
            if (conditionTypeId.HasValue) query = query.Where(p => p.ConditionTypeId == conditionTypeId);
            if (availabilityStatusId.HasValue) query = query.Where(p => p.AvailabilityStatusId == availabilityStatusId);
            if (isActive.HasValue) query = query.Where(p => p.IsActive == isActive);
            if (isPublished.HasValue) query = query.Where(p => p.IsPublished == isPublished);

            var totalCount = await query.CountAsync(ct);
            var items = (IReadOnlyList<ProductEntity>)await query
                .OrderBy(p => p.Name)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            await tx.CommitAsync(ct);
            return (items, totalCount);
        });
    }

    public async Task<ProductEntity> CreateAsync(ProductEntity entity, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            db.Products.Add(entity);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return entity;
    }

    public async Task<ProductEntity> UpdateAsync(ProductEntity entity, CancellationToken ct = default)
    {
        var tenantId = TenantId;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await SetTenantContextAsync(tenantId, ct);
            db.Products.Update(entity);
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
            var entity = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
                ?? throw new KeyNotFoundException($"Product {id} not found.");
            entity.DeletedAt = DateTimeOffset.UtcNow;
            entity.DeleteUserId = deleteUserId;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }
}
