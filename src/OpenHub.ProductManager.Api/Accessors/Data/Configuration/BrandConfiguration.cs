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
        builder.Property(b => b.Name).IsRequired();
        builder.HasQueryFilter(b => b.DeletedAt == null);
    }
}
