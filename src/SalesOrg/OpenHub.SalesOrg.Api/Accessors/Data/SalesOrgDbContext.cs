using Microsoft.EntityFrameworkCore;
using OpenHub.SalesOrg.Api.Accessors.Models;

namespace OpenHub.SalesOrg.Api.Accessors.Data;

public class SalesOrgDbContext(DbContextOptions<SalesOrgDbContext> options) : DbContext(options)
{
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<SalesPersonEntity> SalesPeople => Set<SalesPersonEntity>();
    public DbSet<SalesRoleEntity> SalesRoles => Set<SalesRoleEntity>();
    public DbSet<SalesOrganizationUnitEntity> SalesOrganizationUnits => Set<SalesOrganizationUnitEntity>();
    public DbSet<SalesAssignmentEntity> SalesAssignments => Set<SalesAssignmentEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SalesOrgDbContext).Assembly);
    }
}
