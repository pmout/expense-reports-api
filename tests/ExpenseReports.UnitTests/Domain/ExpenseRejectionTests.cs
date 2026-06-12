using ExpenseReports.Domain.Expenses;

namespace ExpenseReports.UnitTests.Domain;

/// <summary>
/// Business rules 1, 3 and 5 applied to rejection.
/// </summary>
public class ExpenseRejectionTests
{
    private static readonly RejectionReason ValidReason =
        RejectionReason.Of("Receipt is missing from the submission.");

    [Fact]
    public void Manager_of_same_tenant_can_reject_with_reason()
    {
        var expense = Fixture.PendingExpense(Fixture.Employee());
        var manager = Fixture.Manager();

        expense.Reject(manager, ValidReason, Fixture.UtcNow);

        Assert.Equal(ExpenseStatus.Rejected, expense.Status);
        Assert.Equal(ValidReason, expense.RejectionReason);
        Assert.Equal(manager.Id, expense.DecidedByEmployeeId);
        Assert.Equal(Fixture.UtcNow, expense.DecidedAt);
    }

    [Theory] // Rule 5: reason must have 10–500 characters
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("too short")] // 9 characters
    public void Rejection_reason_below_minimum_length_is_invalid(string reason)
    {
        Assert.Throws<InvalidRejectionReasonException>(() => RejectionReason.Of(reason));
    }

    [Fact]
    public void Rejection_reason_over_500_characters_is_invalid()
    {
        Assert.Throws<InvalidRejectionReasonException>(
            () => RejectionReason.Of(new string('x', 501)));
    }

    [Fact] // Rule 1 applies to rejection as well
    public void Non_manager_cannot_reject()
    {
        var expense = Fixture.PendingExpense(Fixture.Employee());
        var colleague = Fixture.Employee(email: "bob@acme.test");

        Assert.Throws<ApproverNotManagerException>(
            () => expense.Reject(colleague, ValidReason, Fixture.UtcNow));
    }

    [Fact] // Rule 1 + 6
    public void Manager_of_another_tenant_cannot_reject()
    {
        var expense = Fixture.PendingExpense(Fixture.Employee());
        var foreignManager = Fixture.Manager(tenantId: Guid.NewGuid());

        Assert.Throws<CrossTenantDecisionException>(
            () => expense.Reject(foreignManager, ValidReason, Fixture.UtcNow));
    }

    [Fact] // Rule 3: a decision is final
    public void Decided_expense_cannot_be_rejected_again()
    {
        var expense = Fixture.PendingExpense(Fixture.Employee());
        var manager = Fixture.Manager();
        expense.Reject(manager, ValidReason, Fixture.UtcNow);

        Assert.Throws<ExpenseAlreadyDecidedException>(
            () => expense.Reject(manager, ValidReason, Fixture.UtcNow));
    }

    [Fact] // Rule 2 covers approval only: rejecting one's own expense is a withdrawal, with no financial effect
    public void Manager_can_reject_own_expense()
    {
        var manager = Fixture.Manager();
        var ownExpense = Fixture.PendingExpense(manager);

        ownExpense.Reject(manager, ValidReason, Fixture.UtcNow);

        Assert.Equal(ExpenseStatus.Rejected, ownExpense.Status);
    }
}
