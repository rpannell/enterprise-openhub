using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OpenHub.ProductManager.Api.Accessors.Data;
using OpenHub.ProductManager.Api.Accessors.Interfaces;
using OpenHub.ProductManager.Api.Accessors.Models;

namespace OpenHub.ProductManager.Api.Accessors;

public class ProductAccessor(CatalogDbContext db, IHttpContextAccessor http) : IProductAccessor
{
    private async Task SetTenantContextAsync(CancellationToken ct)
    {
        var tenantId = http.HttpContext?.Items["TenantId"] as Guid?
            ?? throw new InvalidOperationException("Tenant context not set. Ensure TenantMiddleware is registered.");

        await db.Database.ExecuteSqlRawAsync(
            "SET LOCAL app.current_tenant_id = {0}", [tenantId.ToString()], ct);
    }

    public async Task<ProductEntity?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await SetTenantContextAsync(ct);
        return await db.Products.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<IReadOnlyList<ProductEntity>> GetAllAsync(CancellationToken ct = default)
    {
        await SetTenantContextAsync(ct);
        return await db.Products.AsNoTracking()
            .OrderBy(p => p.Name)
            .ToListAsync(ct);
    }

    public async Task<ProductEntity> CreateAsync(ProductEntity entity, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await SetTenantContextAsync(ct);

        db.Products.Add(entity);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return entity;
    }

    public async Task<ProductEntity> UpdateAsync(ProductEntity entity, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await SetTenantContextAsync(ct);

        db.Products.Update(entity);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return entity;
    }

    public async Task SoftDeleteAsync(Guid id, Guid deleteUserId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await SetTenantContextAsync(ct);

        var entity = await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException($"Product {id} not found.");

        entity.DeletedAt = DateTimeOffset.UtcNow;
        entity.DeleteUserId = deleteUserId;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
