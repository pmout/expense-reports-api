using ExpenseReports.Domain.Tenants;
using ExpenseReports.Domain.ValueObjects;

namespace ExpenseReports.UnitTests.Domain;

public class MoneyTests
{
    [Fact]
    public void Negative_amount_is_invalid()
    {
        Assert.Throws<InvalidMoneyException>(() => Money.Of(-1m, Currency.EUR));
    }

    [Fact]
    public void More_than_two_decimal_places_is_invalid()
    {
        Assert.Throws<InvalidMoneyException>(() => Money.Of(10.999m, Currency.EUR));
    }

    [Fact]
    public void Adding_same_currency_sums_amounts()
    {
        var total = Money.Of(10.50m, Currency.BRL).Add(Money.Of(4.50m, Currency.BRL));

        Assert.Equal(Money.Of(15m, Currency.BRL), total);
    }

    [Fact]
    public void Adding_different_currencies_is_refused()
    {
        Assert.Throws<CurrencyMismatchException>(
            () => Money.Of(10m, Currency.EUR).Add(Money.Of(10m, Currency.USD)));
    }
}

public class EmailTests
{
    [Fact]
    public void Email_is_trimmed_and_lower_cased()
    {
        var email = Email.Of("  Alice@Acme.TEST ");

        Assert.Equal("alice@acme.test", email.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing@tld")]
    [InlineData("two@@signs.test")]
    public void Malformed_email_is_invalid(string value)
    {
        Assert.Throws<InvalidEmailException>(() => Email.Of(value));
    }
}

public class TenantTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void Monthly_limit_must_be_positive(decimal limit)
    {
        Assert.Throws<InvalidTenantException>(() => Tenant.Create("Acme", limit));
    }

    [Fact]
    public void Tenant_name_is_required()
    {
        Assert.Throws<InvalidTenantException>(() => Tenant.Create("   ", 1000m));
    }
}
