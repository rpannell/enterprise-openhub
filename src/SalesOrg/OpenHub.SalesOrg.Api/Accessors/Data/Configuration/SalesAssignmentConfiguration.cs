using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHub.SalesOrg.Api.Accessors.Models;

namespace OpenHub.SalesOrg.Api.Accessors.Data.Configuration;

public class SalesAssignmentConfiguration : IEntityTypeConfiguration<SalesAssignmentEntity>
{
    public void Configure(EntityTypeBuilder<SalesAssignmentEntity> builder)
    {
        builder.ToTable("sales_assignments", "sales_org");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.TenantId).HasColumnName("tenant_id");
        builder.Property(a => a.SalesPersonId).HasColumnName("sales_person_id");
        builder.Property(a => a.OrganizationUnitId).HasColumnName("organization_unit_id");
        builder.Property(a => a.RoleId).HasColumnName("role_id");
        builder.Property(a => a.EffectiveStartDate).HasColumnName("effective_start_date");
        builder.Property(a => a.EffectiveEndDate).HasColumnName("effective_end_date");
        builder.Property(a => a.PrimaryAssignmentFlag).HasColumnName("primary_assignment_flag");
        builder.Property(a => a.ActiveStatus).HasColumnName("active_status");
        builder.Property(a => a.CreateUserId).HasColumnName("create_user_id");
        builder.Property(a => a.ModifyUserId).HasColumnName("modify_user_id");
        builder.Property(a => a.DeleteUserId).HasColumnName("delete_user_id");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne<SalesPersonEntity>().WithMany().HasForeignKey(a => a.SalesPersonId);
        builder.HasOne<SalesOrganizationUnitEntity>().WithMany().HasForeignKey(a => a.OrganizationUnitId);
        builder.HasOne<SalesRoleEntity>().WithMany().HasForeignKey(a => a.RoleId);

        builder.HasQueryFilter(a => a.DeletedAt == null);
    }
}
