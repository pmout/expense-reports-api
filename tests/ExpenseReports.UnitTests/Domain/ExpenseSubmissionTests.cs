using ExpenseReports.Domain.Expenses;
using ExpenseReports.Domain.ValueObjects;

namespace ExpenseReports.UnitTests.Domain;

public class ExpenseSubmissionTests
{
    [Fact]
    public void Submit_with_valid_data_creates_pending_expense_owned_by_employee()
    {
        var owner = Fixture.Employee();

        var expense = Fixture.PendingExpense(owner, amount: 42.50m);

        Assert.Equal(ExpenseStatus.Pending, expense.Status);
        Assert.Equal(owner.Id, expense.EmployeeId);
        Assert.Equal(owner.TenantId, expense.TenantId);
        Assert.Equal(Fixture.UtcNow, expense.SubmittedAt);
        Assert.Null(expense.DecidedAt);
        Assert.Null(expense.DecidedByEmployeeId);
        Assert.Null(expense.RejectionReason);
    }

    [Fact]
    public void Submit_with_zero_amount_is_rejected()
    {
        var owner = Fixture.Employee();

        Assert.Throws<InvalidExpenseException>(() => Fixture.PendingExpense(owner, amount: 0m));
    }

    [Theory]
    [InlineData("1234")] // below the 5-character minimum
    [InlineData("")]
    public void Submit_with_too_short_description_is_rejected(string description)
    {
        var owner = Fixture.Employee();

        Assert.Throws<InvalidExpenseException>(() => Expense.Submit(
            owner, Money.Of(10m, Currency.EUR), ExpenseCategory.Other,
            description, Fixture.Today, Fixture.UtcNow));
    }

    [Fact]
    public void Submit_with_description_over_500_characters_is_rejected()
    {
        var owner = Fixture.Employee();

        Assert.Throws<InvalidExpenseException>(() => Expense.Submit(
            owner, Money.Of(10m, Currency.EUR), ExpenseCategory.Other,
            new string('x', 501), Fixture.Today, Fixture.UtcNow));
    }

    [Fact]
    public void Submit_with_future_date_is_rejected()
    {
        var owner = Fixture.Employee();

        Assert.Throws<InvalidExpenseException>(
            () => Fixture.PendingExpense(owner, expenseDate: Fixture.Today.AddDays(1)));
    }

    [Fact]
    public void Submit_with_date_older_than_90_days_is_rejected()
    {
        var owner = Fixture.Employee();

        Assert.Throws<InvalidExpenseException>(
            () => Fixture.PendingExpense(owner, expenseDate: Fixture.Today.AddDays(-91)));
    }

    [Fact]
    public void Submit_at_the_date_window_boundaries_is_accepted()
    {
        var owner = Fixture.Employee();

        var todayExpense = Fixture.PendingExpense(owner, expenseDate: Fixture.Today);
        var oldestAllowed = Fixture.PendingExpense(owner, expenseDate: Fixture.Today.AddDays(-90));

        Assert.Equal(ExpenseStatus.Pending, todayExpense.Status);
        Assert.Equal(ExpenseStatus.Pending, oldestAllowed.Status);
    }
}
