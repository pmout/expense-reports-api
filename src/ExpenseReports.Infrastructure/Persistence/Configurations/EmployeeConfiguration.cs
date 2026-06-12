using ExpenseReports.Domain.Employees;
using ExpenseReports.Domain.Tenants;
using ExpenseReports.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseReports.Infrastructure.Persistence.Configurations;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employees");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();

        builder.Property(e => e.Email)
            .HasConversion(email => email.Value, value => Email.Of(value))
            .HasMaxLength(320)
            .IsRequired();

        // Globally unique (not per tenant): e-mail is the login identifier and
        // the login lookup runs before any tenant is known.
        builder.HasIndex(e => e.Email).IsUnique();

        builder.Property(e => e.Role).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.PasswordHash).HasMaxLength(100).IsRequired();

        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(e => e.TenantId);
    }
}
