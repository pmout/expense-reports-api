using ExpenseReports.Application.Abstractions;
using ExpenseReports.Application.Common;
using ExpenseReports.Domain.Employees;

namespace ExpenseReports.Application.Expenses;

/// <summary>
/// Detail of a single expense. Visible to its owner and to managers of the
/// same tenant; everyone else gets a 404 (not 403 — see NotFoundException).
/// </summary>
public sealed class GetExpenseHandler(ICurrentUser currentUser, IExpenseRepository expenses)
{
    public async Task<ExpenseResponse> HandleAsync(Guid expenseId, CancellationToken ct)
    {
        // The repository's tenant filter already excludes other tenants, so a hit
        // here is guaranteed to be in the caller's tenant.
        var expense = await expenses.GetByIdAsync(expenseId, ct)
            ?? throw new NotFoundException("Expense");

        // Within the tenant, an employee may see only their own expense; a manager
        // may see any. A non-owner non-manager gets 404, not 403, so they cannot
        // even confirm the expense exists.
        var isOwner = expense.EmployeeId == currentUser.EmployeeId;
        if (!isOwner && currentUser.Role != Role.Manager)
            throw new NotFoundException("Expense");

        return ExpenseResponse.From(expense);
    }
}

public sealed class ListMyExpensesHandler(ICurrentUser currentUser, IExpenseRepository expenses)
{
    public async Task<Page<ExpenseResponse>> HandleAsync(PageRequest page, CancellationToken ct)
    {
        // Scoped to the caller's own id — "my expenses", regardless of role.
        var result = await expenses.ListByEmployeeAsync(currentUser.EmployeeId, page, ct);
        return result.Map(ExpenseResponse.From);
    }
}

public sealed class ListPendingExpensesHandler(IExpenseRepository expenses)
{
    public async Task<Page<ExpenseResponse>> HandleAsync(PageRequest page, CancellationToken ct)
    {
        var result = await expenses.ListPendingAsync(page, ct);
        return result.Map(ExpenseResponse.From);
    }
}
