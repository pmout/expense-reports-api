using ExpenseReports.Domain.Employees;
using ExpenseReports.Domain.Expenses;
using ExpenseReports.Domain.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ExpenseReports.Infrastructure.Persistence.Configurations;

// Mapping kept in a separate class (not in OnModelCreating) so each entity's
// configuration is isolated and discovered automatically. This is also where the
// persistence concerns live, keeping the domain model free of EF Core attributes.
internal sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expenses");

        builder.HasKey(e => e.Id);

        // Money is an owned value object: it has no table of its own, its fields
        // become columns on `expenses`. numeric(12,2) stores the amount as exact
        // decimal — never float, which would introduce rounding errors in money.
        builder.OwnsOne(e => e.Amount, money =>
        {
            money.Property(m => m.Amount).HasColumnName("amount").HasColumnType("numeric(12,2)");
            money.Property(m => m.Currency).HasColumnName("currency").HasConversion<string>().HasMaxLength(3);
        });

        // Get-only properties are not picked up by convention; map it explicitly.
        builder.Property(e => e.SubmittedAt).IsRequired();

        // Enums persisted as text (HasConversion<string>) rather than ints: the
        // database stays readable and is not tied to the enum's numeric values.
        builder.Property(e => e.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.Description).HasMaxLength(Expense.DescriptionMaxLength).IsRequired();

        // Store the RejectionReason value object as a plain string column, going
        // back through Of() on read so an invalid value can never load silently.
        builder.Property(e => e.RejectionReason)
            .HasConversion(reason => reason!.Value, value => RejectionReason.Of(value))
            .HasMaxLength(RejectionReason.MaxLength);

        // Postgres' xmin system column: optimistic concurrency token that makes
        // the Pending -> Approved/Rejected transition race-safe (rule 3). If two
        // requests load the same expense, the second SaveChanges sees a changed
        // xmin and throws DbUpdateConcurrencyException -> mapped to 409.
        builder.Property<uint>("Version").IsRowVersion();

        // Restrict (not Cascade): deleting a tenant or employee must not silently
        // wipe their expenses — that would be data loss hiding behind a delete.
        builder.HasOne<Tenant>().WithMany().HasForeignKey(e => e.TenantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Employee>().WithMany().HasForeignKey(e => e.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        // Indexes lead with TenantId because every query is tenant-filtered, then
        // add the columns each query actually filters on: Status for the pending
        // list, and (Employee, Status, ExpenseDate) for the monthly-total sum.
        builder.HasIndex(e => new { e.TenantId, e.Status });
        builder.HasIndex(e => new { e.TenantId, e.EmployeeId, e.Status, e.ExpenseDate });
    }
}
