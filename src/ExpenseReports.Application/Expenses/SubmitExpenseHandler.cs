using ExpenseReports.Application.Abstractions;
using ExpenseReports.Application.Common;
using ExpenseReports.Domain.Expenses;
using ExpenseReports.Domain.ValueObjects;

namespace ExpenseReports.Application.Expenses;

public sealed record SubmitExpenseRequest(
    decimal Amount,
    Currency Currency,
    ExpenseCategory Category,
    string Description,
    DateOnly ExpenseDate);

public sealed class SubmitExpenseHandler(
    ICurrentUser currentUser,
    IEmployeeRepository employees,
    IExpenseRepository expenses,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<ExpenseResponse> HandleAsync(SubmitExpenseRequest request, CancellationToken ct)
    {
        var employee = await employees.GetByIdAsync(currentUser.EmployeeId, ct)
            ?? throw new NotFoundException("Employee");

        var expense = Expense.Submit(
            employee,
            Money.Of(request.Amount, request.Currency),
            request.Category,
            request.Description,
            request.ExpenseDate,
            clock.GetUtcNow());

        await expenses.AddAsync(expense, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return ExpenseResponse.From(expense);
    }
}
