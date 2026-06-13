using ExpenseReports.Application.Abstractions;
using ExpenseReports.Domain.Auditing;

namespace ExpenseReports.Infrastructure.Persistence.Repositories;

// Append-only: the only operation is adding an entry. The SaveChanges that
// persists it is owned by the handler, so the write joins the decision's
// transaction rather than committing on its own.
internal sealed class ExpenseAuditRepository(ExpenseReportsDbContext db) : IExpenseAuditRepository
{
    public async Task AddAsync(ExpenseAuditEntry entry, CancellationToken ct) =>
        await db.AuditEntries.AddAsync(entry, ct);
}
