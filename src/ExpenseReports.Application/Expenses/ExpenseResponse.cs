using ExpenseReports.Domain.Expenses;

namespace ExpenseReports.Application.Expenses;

public sealed record ExpenseResponse(
    Guid Id,
    Guid EmployeeId,
    decimal Amount,
    string Currency,
    string Category,
    string Description,
    DateOnly ExpenseDate,
    string Status,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? DecidedAt,
    Guid? DecidedByEmployeeId,
    string? RejectionReason)
{
    public static ExpenseResponse From(Expense expense) => new(
        expense.Id,
        expense.EmployeeId,
        expense.Amount.Amount,
        expense.Amount.Currency.ToString(),
        expense.Category.ToString(),
        expense.Description,
        expense.ExpenseDate,
        expense.Status.ToString(),
        expense.SubmittedAt,
        expense.DecidedAt,
        expense.DecidedByEmployeeId,
        expense.RejectionReason?.Value);
}
