using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHub.CRM.Api.Accessors.Models;

namespace OpenHub.CRM.Api.Accessors.Data.Configuration;

public class ClientContactPhoneConfiguration : IEntityTypeConfiguration<ClientContactPhoneEntity>
{
    public void Configure(EntityTypeBuilder<ClientContactPhoneEntity> builder)
    {
        builder.ToTable("client_contact_phones", "crm");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).HasColumnName("id");
        builder.Property(p => p.TenantId).HasColumnName("tenant_id");
        builder.Property(p => p.ContactId).HasColumnName("contact_id");
        builder.Property(p => p.PhoneType).HasColumnName("phone_type").IsRequired();
        builder.Property(p => p.PhoneNumber).HasColumnName("phone_number").IsRequired();
        builder.Property(p => p.IsPrimary).HasColumnName("is_primary");
        builder.Property(p => p.CreatedAt).HasColumnName("created_at");
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");
        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne<TenantEntity>().WithMany().HasForeignKey(p => p.TenantId);
        builder.HasOne<ClientContactEntity>().WithMany().HasForeignKey(p => p.ContactId);

        builder.HasQueryFilter(p => p.DeletedAt == null);
    }
}
