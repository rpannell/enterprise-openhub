using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHub.ProductManager.Api.Accessors.Models;

namespace OpenHub.ProductManager.Api.Accessors.Data.Configuration;

public class ManufacturerConfiguration : IEntityTypeConfiguration<ManufacturerEntity>
{
    public void Configure(EntityTypeBuilder<ManufacturerEntity> builder)
    {
        builder.ToTable("manufacturers", "catalog");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.TenantId).HasColumnName("tenant_id");
        builder.Property(m => m.Name).HasColumnName("name").IsRequired();
        builder.Property(m => m.Code).HasColumnName("code");
        builder.Property(m => m.CountryCode).HasColumnName("country_code");
        builder.Property(m => m.WebsiteUrl).HasColumnName("website_url");
        builder.Property(m => m.IsActive).HasColumnName("is_active");
        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.DeletedAt).HasColumnName("deleted_at");

        builder.HasQueryFilter(m => m.DeletedAt == null);
    }
}
