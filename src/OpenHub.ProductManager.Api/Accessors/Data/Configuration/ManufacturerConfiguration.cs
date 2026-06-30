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
        builder.Property(m => m.Name).IsRequired();
        builder.HasQueryFilter(m => m.DeletedAt == null);
    }
}
