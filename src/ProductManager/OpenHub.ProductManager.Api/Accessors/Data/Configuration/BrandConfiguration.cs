using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHub.ProductManager.Api.Accessors.Models;

namespace OpenHub.ProductManager.Api.Accessors.Data.Configuration;

public class BrandConfiguration : IEntityTypeConfiguration<BrandEntity>
{
    public void Configure(EntityTypeBuilder<BrandEntity> builder)
    {
        builder.ToTable("brands", "catalog");
        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).HasColumnName("id");
        builder.Property(b => b.TenantId).HasColumnName("tenant_id");
        builder.Property(b => b.ManufacturerId).HasColumnName("manufacturer_id");
        builder.Property(b => b.Name).HasColumnName("name").IsRequired();
        builder.Property(b => b.Code).HasColumnName("code");
        builder.Property(b => b.LogoUrl).HasColumnName("logo_url");
        builder.Property(b => b.IsActive).HasColumnName("is_active");
        builder.Property(b => b.CreatedAt).HasColumnName("created_at");
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");
        builder.Property(b => b.DeletedAt).HasColumnName("deleted_at");

        builder.HasQueryFilter(b => b.DeletedAt == null);
    }
}
