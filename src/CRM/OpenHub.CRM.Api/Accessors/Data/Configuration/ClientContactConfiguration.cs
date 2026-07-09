using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHub.CRM.Api.Accessors.Models;

namespace OpenHub.CRM.Api.Accessors.Data.Configuration;

public class ClientContactConfiguration : IEntityTypeConfiguration<ClientContactEntity>
{
    public void Configure(EntityTypeBuilder<ClientContactEntity> builder)
    {
        builder.ToTable("client_contacts", "crm");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.ClientId).HasColumnName("client_id");
        builder.Property(c => c.FirstName).HasColumnName("first_name").IsRequired();
        builder.Property(c => c.LastName).HasColumnName("last_name").IsRequired();
        builder.Property(c => c.Title).HasColumnName("title");
        builder.Property(c => c.Email).HasColumnName("email");
        builder.Property(c => c.IsPrimary).HasColumnName("is_primary");
        builder.Property(c => c.Notes).HasColumnName("notes");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne<TenantEntity>().WithMany().HasForeignKey(c => c.TenantId);
        builder.HasOne<ClientEntity>().WithMany().HasForeignKey(c => c.ClientId);

        builder.HasQueryFilter(c => c.DeletedAt == null);
    }
}
