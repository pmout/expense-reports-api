using ExpenseReports.Domain.Auditing;
using ExpenseReports.Domain.Employees;
using ExpenseReports.Domain.Expenses;
using ExpenseReports.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseReports.Infrastructure.Persistence.Configurations;

internal sealed class ExpenseAuditEntryConfiguration : IEntityTypeConfiguration<ExpenseAuditEntry>
{
    public void Configure(EntityTypeBuilder<ExpenseAuditEntry> builder)
    {
        builder.ToTable("expense_audit_entries");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Decision).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Reason).HasMaxLength(RejectionReason.MaxLength);
        // Get-only properties not referenced as keys are not mapped by convention,
        // so DecidedAt is mapped explicitly (as SubmittedAt is on Expense).
        builder.Property(a => a.DecidedAt).IsRequired();

        // No concurrency token and no updatable state: this table is append-only,
        // so there is nothing to race on. Restrict deletes like the other tables.
        // Two relationships to employees: the expense owner and the decider.
        builder.HasOne<Tenant>().WithMany().HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Expense>().WithMany().HasForeignKey(a => a.ExpenseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(a => a.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(a => a.DecidedByEmployeeId).OnDelete(DeleteBehavior.Restrict);

        // Lead with TenantId (every read is tenant-scoped); ExpenseId next, since
        // "show the audit trail of this expense" is the natural query.
        builder.HasIndex(a => new { a.TenantId, a.ExpenseId });
    }
}
