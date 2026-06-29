using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using OpenHub.ProductManager.Api.Models.Entities;

namespace OpenHub.ProductManager.Api.Accessors.Data.Configuration;

public class ProductConfiguration : IEntityTypeConfiguration<ProductEntity>
{
    public void Configure(EntityTypeBuilder<ProductEntity> builder)
    {
        builder.ToTable("products", "catalog");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Sku).IsRequired();
        builder.Property(p => p.Name).IsRequired();
        builder.Property(p => p.SysPeriod).HasColumnType("tstzrange");
        builder.Property(p => p.Weight).HasPrecision(10, 4);
        builder.HasQueryFilter(p => p.DeletedAt == null);
    }
}
