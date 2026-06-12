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
        var expense = await expenses.GetByIdAsync(expenseId, ct)
            ?? throw new NotFoundException("Expense");

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
