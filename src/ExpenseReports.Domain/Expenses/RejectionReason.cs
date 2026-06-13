using ExpenseReports.Domain.Common;

namespace ExpenseReports.Domain.Expenses;

/// <summary>
/// Mandatory justification for a rejected expense (10–500 characters).
/// </summary>
// A value object instead of a plain string: holding a RejectionReason is itself
// proof the text is valid, so Expense.Reject cannot be called with a bad reason.
public sealed record RejectionReason
{
    // Public consts so the same limits are reused by the API validators — the
    // length rule is defined in exactly one place and the two layers cannot drift.
    public const int MinLength = 10;
    public const int MaxLength = 500;

    public string Value { get; }

    private RejectionReason(string value) => Value = value;

    public static RejectionReason Of(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length is < MinLength or > MaxLength)
            throw new InvalidRejectionReasonException();

        return new RejectionReason(trimmed);
    }

    public override string ToString() => Value;
}

public sealed class InvalidRejectionReasonException()
    : DomainException($"Rejection reason must have between {RejectionReason.MinLength} and {RejectionReason.MaxLength} characters.");
