using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHub.ProductManager.Api.Accessors.Models;

namespace OpenHub.ProductManager.Api.Accessors.Data.Configuration;

public class TenantConfiguration : IEntityTypeConfiguration<TenantEntity>
{
    public void Configure(EntityTypeBuilder<TenantEntity> builder)
    {
        builder.ToTable("tenants", "catalog");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Slug).IsRequired();
        builder.Property(t => t.Name).IsRequired();
        builder.HasIndex(t => t.Slug).IsUnique();
    }
}
