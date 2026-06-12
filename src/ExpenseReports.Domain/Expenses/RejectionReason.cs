using ExpenseReports.Domain.Common;

namespace ExpenseReports.Domain.Expenses;

/// <summary>
/// Mandatory justification for a rejected expense (10–500 characters).
/// </summary>
public sealed record RejectionReason
{
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
