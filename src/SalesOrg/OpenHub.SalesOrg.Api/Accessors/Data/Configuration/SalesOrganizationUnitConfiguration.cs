using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHub.SalesOrg.Api.Accessors.Models;

namespace OpenHub.SalesOrg.Api.Accessors.Data.Configuration;

public class SalesOrganizationUnitConfiguration : IEntityTypeConfiguration<SalesOrganizationUnitEntity>
{
    public void Configure(EntityTypeBuilder<SalesOrganizationUnitEntity> builder)
    {
        builder.ToTable("sales_organization_units", "sales_org");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.TenantId).HasColumnName("tenant_id");
        builder.Property(u => u.Name).HasColumnName("name").IsRequired();
        builder.Property(u => u.Description).HasColumnName("description");
        builder.Property(u => u.OrganizationUnitType).HasColumnName("organization_unit_type").IsRequired();
        builder.Property(u => u.ParentOrganizationUnitId).HasColumnName("parent_organization_unit_id");
        builder.Property(u => u.ActiveStatus).HasColumnName("active_status");
        builder.Property(u => u.CreateUserId).HasColumnName("create_user_id");
        builder.Property(u => u.ModifyUserId).HasColumnName("modify_user_id");
        builder.Property(u => u.DeleteUserId).HasColumnName("delete_user_id");
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.Property(u => u.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne<SalesOrganizationUnitEntity>().WithMany()
            .HasForeignKey(u => u.ParentOrganizationUnitId);

        builder.HasQueryFilter(u => u.DeletedAt == null);
    }
}
