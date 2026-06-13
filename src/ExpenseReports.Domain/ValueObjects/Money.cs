using ExpenseReports.Domain.Common;

namespace ExpenseReports.Domain.ValueObjects;

/// <summary>
/// An amount in a specific currency. Arithmetic across different currencies is
/// rejected: there is no exchange-rate concept in this domain.
/// </summary>
// `record` gives value equality for free: two Money with the same amount and
// currency are equal, which is exactly how a value object should behave (unlike
// an entity, it has no identity). `sealed` because value objects are not meant
// to be extended — subclassing would break their equality contract.
public sealed record Money
{
    // Get-only properties make the type immutable: once created it never changes,
    // so it can be shared freely and can never drift into an invalid state.
    public decimal Amount { get; }
    public Currency Currency { get; }

    // Private constructor forces every instance through the Of factory below,
    // which is the single point where validation happens. No caller can bypass it.
    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// The only way to construct money: rejects negatives and sub-cent precision,
    /// so an invalid amount can never reach the rest of the system.
    /// </summary>
    public static Money Of(decimal amount, Currency currency)
    {
        if (amount < 0)
            throw new InvalidMoneyException("Amount cannot be negative.");
        if (decimal.Round(amount, 2) != amount)
            throw new InvalidMoneyException("Amount cannot have more than two decimal places.");

        return new Money(amount, currency);
    }

    public static Money Zero(Currency currency) => new(0m, currency);

    /// <summary>
    /// Adds two amounts, refusing to combine different currencies. This is what
    /// makes the monthly limit currency-safe: there is no implicit conversion.
    /// </summary>
    public Money Add(Money other)
    {
        if (other.Currency != Currency)
            throw new CurrencyMismatchException(Currency, other.Currency);

        return new Money(Amount + other.Amount, Currency);
    }

    public override string ToString() => $"{Amount:0.00} {Currency}";
}

public sealed class InvalidMoneyException(string message) : DomainException(message);

public sealed class CurrencyMismatchException(Currency expected, Currency actual)
    : DomainException($"Cannot operate on {actual} amounts together with {expected} amounts.");
