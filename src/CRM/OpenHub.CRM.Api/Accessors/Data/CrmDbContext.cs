using Microsoft.EntityFrameworkCore;
using OpenHub.CRM.Api.Accessors.Models;

namespace OpenHub.CRM.Api.Accessors.Data;

public class CrmDbContext(DbContextOptions<CrmDbContext> options) : DbContext(options)
{
    public DbSet<TenantEntity> Tenants => Set<TenantEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<ClientEntity> Clients => Set<ClientEntity>();
    public DbSet<ClientAddressEntity> ClientAddresses => Set<ClientAddressEntity>();
    public DbSet<ClientContactEntity> ClientContacts => Set<ClientContactEntity>();
    public DbSet<ClientContactPhoneEntity> ClientContactPhones => Set<ClientContactPhoneEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
    }
}
