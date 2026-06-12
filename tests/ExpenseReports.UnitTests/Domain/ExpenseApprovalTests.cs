using ExpenseReports.Domain.Expenses;
using ExpenseReports.Domain.ValueObjects;

namespace ExpenseReports.UnitTests.Domain;

/// <summary>
/// Business rules 1–4: who can approve, no self-approval, single state
/// transition and the per-currency monthly limit.
/// </summary>
public class ExpenseApprovalTests
{
    private const decimal MonthlyLimit = 1000m;

    private static readonly Money NothingApprovedYet = Money.Zero(Currency.EUR);

    [Fact]
    public void Manager_of_same_tenant_can_approve()
    {
        var expense = Fixture.PendingExpense(Fixture.Employee());
        var manager = Fixture.Manager();

        expense.Approve(manager, NothingApprovedYet, MonthlyLimit, Fixture.UtcNow);

        Assert.Equal(ExpenseStatus.Approved, expense.Status);
        Assert.Equal(manager.Id, expense.DecidedByEmployeeId);
        Assert.Equal(Fixture.UtcNow, expense.DecidedAt);
    }

    [Fact] // Rule 1: only managers approve
    public void Non_manager_cannot_approve()
    {
        var expense = Fixture.PendingExpense(Fixture.Employee());
        var colleague = Fixture.Employee(email: "bob@acme.test");

        Assert.Throws<ApproverNotManagerException>(
            () => expense.Approve(colleague, NothingApprovedYet, MonthlyLimit, Fixture.UtcNow));
        Assert.Equal(ExpenseStatus.Pending, expense.Status);
    }

    [Fact] // Rule 1 + 6: decisions never cross tenant boundaries
    public void Manager_of_another_tenant_cannot_approve()
    {
        var expense = Fixture.PendingExpense(Fixture.Employee());
        var foreignManager = Fixture.Manager(tenantId: Guid.NewGuid());

        Assert.Throws<CrossTenantDecisionException>(
            () => expense.Approve(foreignManager, NothingApprovedYet, MonthlyLimit, Fixture.UtcNow));
        Assert.Equal(ExpenseStatus.Pending, expense.Status);
    }

    [Fact] // Rule 2
    public void Manager_cannot_approve_own_expense()
    {
        var manager = Fixture.Manager();
        var ownExpense = Fixture.PendingExpense(manager);

        Assert.Throws<SelfApprovalException>(
            () => ownExpense.Approve(manager, NothingApprovedYet, MonthlyLimit, Fixture.UtcNow));
    }

    [Theory] // Rule 3: a decision is final
    [InlineData(true)]
    [InlineData(false)]
    public void Decided_expense_cannot_be_approved_again(bool firstDecisionWasApproval)
    {
        var expense = Fixture.PendingExpense(Fixture.Employee());
        var manager = Fixture.Manager();
        if (firstDecisionWasApproval)
            expense.Approve(manager, NothingApprovedYet, MonthlyLimit, Fixture.UtcNow);
        else
            expense.Reject(manager, RejectionReason.Of("Missing the receipt."), Fixture.UtcNow);

        Assert.Throws<ExpenseAlreadyDecidedException>(
            () => expense.Approve(manager, NothingApprovedYet, MonthlyLimit, Fixture.UtcNow));
    }

    [Fact] // Rule 4
    public void Approval_that_would_exceed_the_monthly_limit_is_refused()
    {
        var expense = Fixture.PendingExpense(Fixture.Employee(), amount: 200m);
        var manager = Fixture.Manager();
        var alreadyApproved = Money.Of(900m, Currency.EUR);

        Assert.Throws<MonthlyLimitExceededException>(
            () => expense.Approve(manager, alreadyApproved, MonthlyLimit, Fixture.UtcNow));
        Assert.Equal(ExpenseStatus.Pending, expense.Status);
    }

    [Fact] // Rule 4 boundary: reaching the limit exactly is allowed
    public void Approval_that_lands_exactly_on_the_monthly_limit_is_allowed()
    {
        var expense = Fixture.PendingExpense(Fixture.Employee(), amount: 100m);
        var manager = Fixture.Manager();
        var alreadyApproved = Money.Of(900m, Currency.EUR);

        expense.Approve(manager, alreadyApproved, MonthlyLimit, Fixture.UtcNow);

        Assert.Equal(ExpenseStatus.Approved, expense.Status);
    }

    [Fact] // Amounts in different currencies are never summed (no FX in the domain)
    public void Approved_total_in_a_different_currency_cannot_be_combined()
    {
        var expense = Fixture.PendingExpense(Fixture.Employee(), currency: Currency.EUR);
        var manager = Fixture.Manager();
        var totalInOtherCurrency = Money.Of(500m, Currency.USD);

        Assert.Throws<CurrencyMismatchException>(
            () => expense.Approve(manager, totalInOtherCurrency, MonthlyLimit, Fixture.UtcNow));
    }
}
