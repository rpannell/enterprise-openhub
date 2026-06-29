using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHub.ProductManager.Api.Models.Entities;

namespace OpenHub.ProductManager.Api.Accessors.Data.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.ToTable("users", "catalog");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.UniqueId).IsRequired();
        builder.Property(u => u.Email).IsRequired();
        builder.Property(u => u.FirstName).IsRequired();
        builder.Property(u => u.LastName).IsRequired();
        builder.HasQueryFilter(u => u.DeletedAt == null);
    }
}
