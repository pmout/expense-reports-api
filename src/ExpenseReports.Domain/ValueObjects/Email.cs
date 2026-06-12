using System.Text.RegularExpressions;
using ExpenseReports.Domain.Common;

namespace ExpenseReports.Domain.ValueObjects;

/// <summary>
/// A normalized (trimmed, lower-cased) e-mail address.
/// </summary>
public sealed partial record Email
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Of(string value)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;

        if (normalized.Length == 0 || !EmailPattern().IsMatch(normalized))
            throw new InvalidEmailException(value ?? "<null>");

        return new Email(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}

public sealed class InvalidEmailException(string value)
    : DomainException($"'{value}' is not a valid e-mail address.");
