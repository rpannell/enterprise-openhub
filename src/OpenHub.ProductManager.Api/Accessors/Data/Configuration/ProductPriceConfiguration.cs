using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHub.ProductManager.Api.Accessors.Models;

namespace OpenHub.ProductManager.Api.Accessors.Data.Configuration;

public class ProductPriceConfiguration : IEntityTypeConfiguration<ProductPriceEntity>
{
    public void Configure(EntityTypeBuilder<ProductPriceEntity> builder)
    {
        builder.ToTable("product_prices", "catalog");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.TenantId).HasColumnName("tenant_id");
        builder.Property(p => p.ProductId).HasColumnName("product_id");
        builder.Property(p => p.PriceTypeId).HasColumnName("price_type_id");
        builder.Property(p => p.CurrencyId).HasColumnName("currency_id");
        builder.Property(p => p.Amount).HasColumnName("amount").HasPrecision(19, 4);
        builder.Property(p => p.EffectiveFrom).HasColumnName("effective_from");
        builder.Property(p => p.EffectiveUntil).HasColumnName("effective_until");
        builder.Property(p => p.IsActive).HasColumnName("is_active");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at");

        builder.HasQueryFilter(p => p.DeletedAt == null);
    }
}
