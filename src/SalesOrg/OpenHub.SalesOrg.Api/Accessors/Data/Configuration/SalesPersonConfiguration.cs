using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OpenHub.SalesOrg.Api.Accessors.Models;

namespace OpenHub.SalesOrg.Api.Accessors.Data.Configuration;

public class SalesPersonConfiguration : IEntityTypeConfiguration<SalesPersonEntity>
{
    public void Configure(EntityTypeBuilder<SalesPersonEntity> builder)
    {
        builder.ToTable("sales_people", "sales_org");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.TenantId).HasColumnName("tenant_id");
        builder.Property(s => s.FirstName).HasColumnName("first_name").IsRequired();
        builder.Property(s => s.LastName).HasColumnName("last_name").IsRequired();
        builder.Property(s => s.Email).HasColumnName("email").IsRequired();
        builder.Property(s => s.Phone).HasColumnName("phone");
        builder.Property(s => s.JobTitle).HasColumnName("job_title");
        builder.Property(s => s.EmployeeNumber).HasColumnName("employee_number");
        builder.Property(s => s.ActiveStatus).HasColumnName("active_status");
        builder.Property(s => s.StartDate).HasColumnName("start_date");
        builder.Property(s => s.EndDate).HasColumnName("end_date");
        builder.Property(s => s.CreatedAt).HasColumnName("created_at");
        builder.Property(s => s.UpdatedAt).HasColumnName("updated_at");
        builder.Property(s => s.DeletedAt).HasColumnName("deleted_at");

        builder.HasQueryFilter(s => s.DeletedAt == null);
    }
}
