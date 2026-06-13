using ExpenseReports.Application.Auth;
using ExpenseReports.Application.Expenses;
using ExpenseReports.Domain.Expenses;
using FluentValidation;

namespace ExpenseReports.Api.Validation;

/// <summary>
/// Input validation at the edge (400 Problem Details). Length limits reuse the
/// domain's constants so the two layers cannot drift apart; the deeper business
/// rules stay in the domain and surface as 409/422.
/// </summary>
internal sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email).NotEmpty().MaximumLength(320);
        RuleFor(r => r.Password).NotEmpty().MaximumLength(128);
    }
}

internal sealed class SubmitExpenseRequestValidator : AbstractValidator<SubmitExpenseRequest>
{
    public SubmitExpenseRequestValidator()
    {
        RuleFor(r => r.Amount)
            .GreaterThan(0)
            // Matches the numeric(12,2) column so an over-precise amount is a clean
            // 400 here, not a database error deeper down.
            .PrecisionScale(12, 2, ignoreTrailingZeros: true);
        // IsInEnum rejects out-of-range values (e.g. currency=999) at the edge,
        // before the string is ever bound to the enum type.
        RuleFor(r => r.Currency).IsInEnum();
        RuleFor(r => r.Category).IsInEnum();
        RuleFor(r => r.Description)
            .NotEmpty()
            .Length(Expense.DescriptionMinLength, Expense.DescriptionMaxLength);
        RuleFor(r => r.ExpenseDate).NotEmpty();
        // Note: the date window (not future / not >90 days) is intentionally NOT
        // here — it is a domain rule, validated in Expense.Submit and surfaced as
        // 422. This validator only checks request shape, not business policy.
    }
}

internal sealed class RejectExpenseRequestValidator : AbstractValidator<RejectExpenseRequest>
{
    public RejectExpenseRequestValidator()
    {
        RuleFor(r => r.Reason)
            .NotEmpty()
            .Length(RejectionReason.MinLength, RejectionReason.MaxLength);
    }
}
