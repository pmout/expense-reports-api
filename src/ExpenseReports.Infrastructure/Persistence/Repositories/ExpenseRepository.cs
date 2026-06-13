using ExpenseReports.Application.Abstractions;
using ExpenseReports.Application.Common;
using ExpenseReports.Domain.Expenses;
using ExpenseReports.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ExpenseReports.Infrastructure.Persistence.Repositories;

/// <summary>
/// Every query here is automatically tenant-scoped by the context's global
/// query filter — there is no code path that skips it.
/// </summary>
internal sealed class ExpenseRepository(ExpenseReportsDbContext db) : IExpenseRepository
{
    public async Task AddAsync(Expense expense, CancellationToken ct) =>
        await db.Expenses.AddAsync(expense, ct);

    public Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Expenses.FirstOrDefaultAsync(e => e.Id == id, ct);

    // Both list methods add only their specific predicate; the tenant filter is
    // applied automatically on top, so "pending" already means "pending in this
    // tenant" without the caller (or this code) ever spelling that out.
    public Task<Page<Expense>> ListByEmployeeAsync(Guid employeeId, PageRequest page, CancellationToken ct) =>
        ToPageAsync(db.Expenses.Where(e => e.EmployeeId == employeeId), page, ct);

    public Task<Page<Expense>> ListPendingAsync(PageRequest page, CancellationToken ct) =>
        ToPageAsync(db.Expenses.Where(e => e.Status == ExpenseStatus.Pending), page, ct);

    public async Task<Money> GetApprovedTotalAsync(
        Guid employeeId, Currency currency, int year, int month, CancellationToken ct)
    {
        // Half-open interval [monthStart, nextMonth) over ExpenseDate. Filtering
        // by currency keeps the total summable — the limit is enforced per currency.
        var monthStart = new DateOnly(year, month, 1);
        var nextMonth = monthStart.AddMonths(1);

        var total = await db.Expenses
            .Where(e => e.EmployeeId == employeeId
                && e.Status == ExpenseStatus.Approved
                && e.Amount.Currency == currency
                && e.ExpenseDate >= monthStart
                && e.ExpenseDate < nextMonth)
            .SumAsync(e => e.Amount.Amount, ct);

        return Money.Of(total, currency);
    }

    public Task LockEmployeeForApprovalAsync(Guid employeeId, CancellationToken ct) =>
        // Transaction-scoped advisory lock keyed on the employee: concurrent
        // approvals for the same employee queue up, so the monthly total each
        // one reads already includes the previous approval. Released
        // automatically at commit/rollback. The interpolated string is passed to
        // ExecuteSqlAsync (FormattableString), so employeeId becomes a bound
        // parameter — this is parameterized SQL, not string concatenation.
        db.Database.ExecuteSqlAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({employeeId.ToString()}, 0))", ct);

    // Shared paging helper: counts the full (tenant-filtered) set, then fetches
    // one page. A stable sort — SubmittedAt then Id as a tie-breaker — keeps
    // pages deterministic when two expenses share a timestamp.
    private static async Task<Page<Expense>> ToPageAsync(
        IQueryable<Expense> query, PageRequest page, CancellationToken ct)
    {
        var ordered = query.OrderByDescending(e => e.SubmittedAt).ThenBy(e => e.Id);

        var totalCount = await query.CountAsync(ct);
        var items = await ordered.Skip(page.Skip).Take(page.PageSize).ToListAsync(ct);

        return new Page<Expense>(items, page.Page, page.PageSize, totalCount);
    }
}
