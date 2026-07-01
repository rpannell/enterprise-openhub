using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHub.ProductManager.Api.Accessors.Models;

namespace OpenHub.ProductManager.Api.Accessors.Data.Configuration;

public class ProductConfiguration : IEntityTypeConfiguration<ProductEntity>
{
    public void Configure(EntityTypeBuilder<ProductEntity> builder)
    {
        builder.ToTable("products", "catalog");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.TenantId).HasColumnName("tenant_id");
        builder.Property(p => p.Sku).HasColumnName("sku").IsRequired();
        builder.Property(p => p.Name).HasColumnName("name").IsRequired();
        builder.Property(p => p.Description).HasColumnName("description");
        builder.Property(p => p.ShortDescription).HasColumnName("short_description");
        builder.Property(p => p.ManufacturerId).HasColumnName("manufacturer_id");
        builder.Property(p => p.BrandId).HasColumnName("brand_id");
        builder.Property(p => p.ConditionTypeId).HasColumnName("condition_type_id");
        builder.Property(p => p.AvailabilityStatusId).HasColumnName("availability_status_id");
        builder.Property(p => p.BaseUomId).HasColumnName("base_uom_id");
        builder.Property(p => p.ManufacturerPartNumber).HasColumnName("manufacturer_part_number");
        builder.Property(p => p.IsActive).HasColumnName("is_active");
        builder.Property(p => p.IsPublished).HasColumnName("is_published");
        builder.Property(p => p.Weight).HasColumnName("weight").HasPrecision(10, 4);
        builder.Property(p => p.WeightUomId).HasColumnName("weight_uom_id");
        builder.Property(p => p.CreateUserId).HasColumnName("create_user_id");
        builder.Property(p => p.ModifyUserId).HasColumnName("modify_user_id");
        builder.Property(p => p.DeleteUserId).HasColumnName("delete_user_id");
        builder.Property(p => p.SysPeriod).HasColumnName("sys_period").HasColumnType("tstzrange");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at");

        builder.HasQueryFilter(p => p.DeletedAt == null);
    }
}
