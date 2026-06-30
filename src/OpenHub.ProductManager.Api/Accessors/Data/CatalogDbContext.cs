using Microsoft.EntityFrameworkCore;
using OpenHub.ProductManager.Api.Accessors.Models;

namespace OpenHub.ProductManager.Api.Accessors.Data;

public class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<ProductEntity> Products => Set<ProductEntity>();
    public DbSet<ProductPriceEntity> ProductPrices => Set<ProductPriceEntity>();
    public DbSet<ManufacturerEntity> Manufacturers => Set<ManufacturerEntity>();
    public DbSet<BrandEntity> Brands => Set<BrandEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }
}
