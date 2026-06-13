using ExpenseReports.Application.Abstractions;
using ExpenseReports.Application.Common;
using ExpenseReports.Domain.Expenses;
using ExpenseReports.Domain.ValueObjects;

namespace ExpenseReports.Application.Expenses;

// The request carries no TenantId or EmployeeId: those come from the JWT via the
// loaded employee, so a caller cannot submit on behalf of someone else.
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
        // Load the authenticated employee (tenant-filtered); Submit then stamps
        // the new expense with that employee's id and tenant.
        var employee = await employees.GetByIdAsync(currentUser.EmployeeId, ct)
            ?? throw new NotFoundException("Employee");

        // The domain factory owns all the validation (amount, description, date
        // window). The handler just feeds it the inputs and the current time.
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
