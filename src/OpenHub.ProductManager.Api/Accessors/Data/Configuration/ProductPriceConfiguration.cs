using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHub.ProductManager.Api.Models.Entities;

namespace OpenHub.ProductManager.Api.Accessors.Data.Configuration;

public class ProductPriceConfiguration : IEntityTypeConfiguration<ProductPriceEntity>
{
    public void Configure(EntityTypeBuilder<ProductPriceEntity> builder)
    {
        builder.ToTable("product_prices", "catalog");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Amount).HasPrecision(19, 4);
        builder.HasQueryFilter(p => p.DeletedAt == null);
    }
}
