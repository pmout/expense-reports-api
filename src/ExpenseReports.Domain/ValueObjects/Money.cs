using ExpenseReports.Domain.Common;

namespace ExpenseReports.Domain.ValueObjects;

/// <summary>
/// An amount in a specific currency. Arithmetic across different currencies is
/// rejected: there is no exchange-rate concept in this domain.
/// </summary>
public sealed record Money
{
    public decimal Amount { get; }
    public Currency Currency { get; }

    private Money(decimal amount, Currency currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Of(decimal amount, Currency currency)
    {
        if (amount < 0)
            throw new InvalidMoneyException("Amount cannot be negative.");
        if (decimal.Round(amount, 2) != amount)
            throw new InvalidMoneyException("Amount cannot have more than two decimal places.");

        return new Money(amount, currency);
    }

    public static Money Zero(Currency currency) => new(0m, currency);

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
