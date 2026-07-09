using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHub.CRM.Api.Accessors.Models;

namespace OpenHub.CRM.Api.Accessors.Data.Configuration;

public class ClientConfiguration : IEntityTypeConfiguration<ClientEntity>
{
    public void Configure(EntityTypeBuilder<ClientEntity> builder)
    {
        builder.ToTable("clients", "crm");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TenantId).HasColumnName("tenant_id");
        builder.Property(c => c.SalesPersonId).HasColumnName("sales_person_id");
        builder.Property(c => c.Name).HasColumnName("name").IsRequired();
        builder.Property(c => c.LegalName).HasColumnName("legal_name");
        builder.Property(c => c.KeycloakOrganizationId).HasColumnName("keycloak_organization_id");
        builder.Property(c => c.KeycloakOrganizationAlias).HasColumnName("keycloak_organization_alias");
        builder.Property(c => c.Status).HasColumnName("status").IsRequired();
        builder.Property(c => c.Industry).HasColumnName("industry");
        builder.Property(c => c.Website).HasColumnName("website");
        builder.Property(c => c.Phone).HasColumnName("phone");
        builder.Property(c => c.AnnualRevenue).HasColumnName("annual_revenue").HasPrecision(19, 2);
        builder.Property(c => c.EmployeeCount).HasColumnName("employee_count");
        builder.Property(c => c.LeadSource).HasColumnName("lead_source");
        builder.Property(c => c.AccountTier).HasColumnName("account_tier").IsRequired();
        builder.Property(c => c.FirstContactDate).HasColumnName("first_contact_date");
        builder.Property(c => c.LastActivityAt).HasColumnName("last_activity_at");
        builder.Property(c => c.Notes).HasColumnName("notes");
        builder.Property(c => c.CreateUserId).HasColumnName("create_user_id");
        builder.Property(c => c.ModifyUserId).HasColumnName("modify_user_id");
        builder.Property(c => c.DeleteUserId).HasColumnName("delete_user_id");
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        builder.Property(c => c.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne<TenantEntity>().WithMany().HasForeignKey(c => c.TenantId);
        // SalesPersonId references sales_org.sales_people (a different database/module) — no EF
        // navigation/FK here, matching the bare-UUID column in the Flyway migration. Validity is
        // enforced at the API layer via ISalesOrgApiAccessor.

        builder.HasQueryFilter(c => c.DeletedAt == null);
    }
}
