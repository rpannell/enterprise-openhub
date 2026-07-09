using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHub.CRM.Api.Accessors.Models;

namespace OpenHub.CRM.Api.Accessors.Data.Configuration;

public class ClientAddressConfiguration : IEntityTypeConfiguration<ClientAddressEntity>
{
    public void Configure(EntityTypeBuilder<ClientAddressEntity> builder)
    {
        builder.ToTable("client_addresses", "crm");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id");
        builder.Property(a => a.TenantId).HasColumnName("tenant_id");
        builder.Property(a => a.ClientId).HasColumnName("client_id");
        builder.Property(a => a.AddressType).HasColumnName("address_type").IsRequired();
        builder.Property(a => a.Line1).HasColumnName("line1").IsRequired();
        builder.Property(a => a.Line2).HasColumnName("line2");
        builder.Property(a => a.City).HasColumnName("city").IsRequired();
        builder.Property(a => a.StateProvince).HasColumnName("state_province");
        builder.Property(a => a.PostalCode).HasColumnName("postal_code");
        builder.Property(a => a.CountryCode).HasColumnName("country_code").IsRequired();
        builder.Property(a => a.IsPrimary).HasColumnName("is_primary");
        builder.Property(a => a.CreatedAt).HasColumnName("created_at");
        builder.Property(a => a.UpdatedAt).HasColumnName("updated_at");
        builder.Property(a => a.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne<TenantEntity>().WithMany().HasForeignKey(a => a.TenantId);
        builder.HasOne<ClientEntity>().WithMany().HasForeignKey(a => a.ClientId);

        builder.HasQueryFilter(a => a.DeletedAt == null);
    }
}
