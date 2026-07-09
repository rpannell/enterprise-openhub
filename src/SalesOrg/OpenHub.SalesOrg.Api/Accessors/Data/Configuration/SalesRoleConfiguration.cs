using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHub.SalesOrg.Api.Accessors.Models;

namespace OpenHub.SalesOrg.Api.Accessors.Data.Configuration;

public class SalesRoleConfiguration : IEntityTypeConfiguration<SalesRoleEntity>
{
    public void Configure(EntityTypeBuilder<SalesRoleEntity> builder)
    {
        builder.ToTable("sales_roles", "sales_org");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TenantId).HasColumnName("tenant_id");
        builder.Property(r => r.Name).HasColumnName("name").IsRequired();
        builder.Property(r => r.Description).HasColumnName("description");
        builder.Property(r => r.HierarchyLevel).HasColumnName("hierarchy_level");
        builder.Property(r => r.ActiveStatus).HasColumnName("active_status");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.DeletedAt).HasColumnName("deleted_at");

        builder.HasQueryFilter(r => r.DeletedAt == null);
    }
}
