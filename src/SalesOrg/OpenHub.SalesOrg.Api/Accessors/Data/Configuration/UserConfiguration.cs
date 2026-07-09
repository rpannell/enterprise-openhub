using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHub.SalesOrg.Api.Accessors.Models;

namespace OpenHub.SalesOrg.Api.Accessors.Data.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.ToTable("users", "sales_org");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.TenantId).HasColumnName("tenant_id");
        builder.Property(u => u.UniqueId).HasColumnName("unique_id").IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").IsRequired();
        builder.Property(u => u.FirstName).HasColumnName("first_name").IsRequired();
        builder.Property(u => u.LastName).HasColumnName("last_name").IsRequired();
        builder.Property(u => u.IsActive).HasColumnName("is_active");
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");
        builder.Property(u => u.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne<TenantEntity>().WithMany().HasForeignKey(u => u.TenantId);

        builder.HasQueryFilter(u => u.DeletedAt == null);
    }
}
