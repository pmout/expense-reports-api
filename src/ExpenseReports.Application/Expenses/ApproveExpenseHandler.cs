using ExpenseReports.Application.Abstractions;
using ExpenseReports.Application.Common;
using ExpenseReports.Domain.Auditing;

namespace ExpenseReports.Application.Expenses;

/// <summary>
/// Orchestrates an approval. The handler stays thin: it loads the actors,
/// supplies the data the aggregate cannot compute itself (the monthly total)
/// and lets <see cref="Domain.Expenses.Expense.Approve"/> enforce the rules.
/// Its real job is getting the concurrency control right.
/// </summary>
public sealed class ApproveExpenseHandler(
    ICurrentUser currentUser,
    IExpenseRepository expenses,
    IEmployeeRepository employees,
    ITenantRepository tenants,
    IExpenseAuditRepository auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    /// <remarks>
    /// The whole decision runs in one transaction. The employee-scoped lock
    /// serializes concurrent approvals for the same employee so the
    /// monthly-limit check reads a stable total; a concurrent decision on the
    /// same expense is caught by optimistic concurrency at commit time.
    /// </remarks>
    public Task<ExpenseResponse> HandleAsync(Guid expenseId, CancellationToken ct) =>
        unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            // Tenant filter applies here: an expense from another tenant simply
            // is not found, which the API turns into a 404.
            var expense = await expenses.GetByIdAsync(expenseId, token)
                ?? throw new NotFoundException("Expense");

            // Take the lock before reading the total, so two approvals for the
            // same employee cannot both read a total that excludes the other.
            await expenses.LockEmployeeForApprovalAsync(expense.EmployeeId, token);

            var approver = await employees.GetByIdAsync(currentUser.EmployeeId, token)
                ?? throw new NotFoundException("Employee");
            var tenant = await tenants.GetByIdAsync(currentUser.TenantId, token)
                ?? throw new NotFoundException("Tenant");

            // The aggregate needs the running total to enforce rule 4, but that
            // requires querying sibling expenses — work that belongs to the repo.
            var approvedTotalThisMonth = await expenses.GetApprovedTotalAsync(
                expense.EmployeeId,
                expense.Amount.Currency,
                expense.ExpenseDate.Year,
                expense.ExpenseDate.Month,
                token);

            expense.Approve(approver, approvedTotalThisMonth, tenant.MonthlyExpenseLimit, clock.GetUtcNow());

            // Audit entry written inside the same transaction as the decision, so
            // the record and the state change commit together or not at all.
            await auditLog.AddAsync(ExpenseAuditEntry.Record(expense), token);

            await unitOfWork.SaveChangesAsync(token);
            return ExpenseResponse.From(expense);
        }, ct);
}
