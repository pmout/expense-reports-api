using ExpenseReports.Application.Abstractions;
using ExpenseReports.Application.Common;

namespace ExpenseReports.Application.Expenses;

public sealed class ApproveExpenseHandler(
    ICurrentUser currentUser,
    IExpenseRepository expenses,
    IEmployeeRepository employees,
    ITenantRepository tenants,
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
            var expense = await expenses.GetByIdAsync(expenseId, token)
                ?? throw new NotFoundException("Expense");

            await expenses.LockEmployeeForApprovalAsync(expense.EmployeeId, token);

            var approver = await employees.GetByIdAsync(currentUser.EmployeeId, token)
                ?? throw new NotFoundException("Employee");
            var tenant = await tenants.GetByIdAsync(currentUser.TenantId, token)
                ?? throw new NotFoundException("Tenant");

            var approvedTotalThisMonth = await expenses.GetApprovedTotalAsync(
                expense.EmployeeId,
                expense.Amount.Currency,
                expense.ExpenseDate.Year,
                expense.ExpenseDate.Month,
                token);

            expense.Approve(approver, approvedTotalThisMonth, tenant.MonthlyExpenseLimit, clock.GetUtcNow());

            await unitOfWork.SaveChangesAsync(token);
            return ExpenseResponse.From(expense);
        }, ct);
}
