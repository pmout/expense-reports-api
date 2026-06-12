using ExpenseReports.Domain.Employees;
using ExpenseReports.Domain.Expenses;
using ExpenseReports.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseReports.Infrastructure.Persistence.Configurations;

internal sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expenses");

        builder.HasKey(e => e.Id);

        builder.OwnsOne(e => e.Amount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)");
            money.Property(m => m.Currency).HasColumnName("currency").HasConversion<string>().HasMaxLength(3);
        });

        // Get-only properties are not picked up by convention; map it explicitly.
        builder.Property(e => e.SubmittedAt).IsRequired();

        builder.Property(e => e.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Description).HasMaxLength(Expense.DescriptionMaxLength).IsRequired();

        builder.Property(e => e.RejectionReason)
            .HasConversion(reason => reason!.Value, value => RejectionReason.Of(value))
            .HasMaxLength(RejectionReason.MaxLength);

        // Postgres' xmin system column: optimistic concurrency token that makes
        // the Pending -> Approved/Rejected transition race-safe (rule 3).
        builder.Property<uint>("Version").IsRowVersion();

        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.TenantId, e.Status });
        builder.HasIndex(e => new { e.TenantId, e.EmployeeId, e.Status, e.ExpenseDate });
    }
}
