using System.Text.RegularExpressions;
using ExpenseReports.Domain.Common;

namespace ExpenseReports.Domain.ValueObjects;

/// <summary>
/// A normalized (trimmed, lower-cased) e-mail address.
/// </summary>
// `partial` is required by the source-generated regex below (GeneratedRegex);
// it lets the compiler add the generated method to this type. Wrapping the
// e-mail in a value object means an unvalidated string can never masquerade as
// an address — the type itself is the guarantee.
public sealed partial record Email
{
    public string Value { get; }

    private Email(string value) => Value = value;

    public static Email Of(string value)
    {
        // Normalize before validating and storing, so "Alice@X.com" and
        // "alice@x.com " compare equal and the unique index treats them as one.
        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;

        if (normalized.Length == 0 || !EmailPattern().IsMatch(normalized))
            throw new InvalidEmailException(value ?? "<null>");

        return new Email(normalized);
    }

    public override string ToString() => Value;

    // Source-generated regex: the pattern is compiled at build time rather than
    // at first use, which is faster and validates the pattern during compilation.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}

public sealed class InvalidEmailException(string value)
    : DomainException($"'{value}' is not a valid e-mail address.");
