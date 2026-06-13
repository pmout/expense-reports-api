using ExpenseReports.Domain.Expenses;

namespace ExpenseReports.Application.Expenses;

// A read DTO, kept separate from the Expense entity on purpose: the API contract
// is decoupled from the domain model (the wire shape can change independently),
// value objects are flattened to primitives (Money -> amount + currency string),
// and only what a client should see is exposed — no domain behavior leaks out.
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
