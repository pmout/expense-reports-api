using ExpenseReports.Domain.Auditing;
using ExpenseReports.Domain.Expenses;
using ExpenseReports.Domain.ValueObjects;

namespace ExpenseReports.UnitTests.Domain;

public class ExpenseAuditEntryTests
{
    private const decimal MonthlyLimit = 1000m;

    [Fact]
    public void Record_of_an_approved_expense_captures_the_decision()
    {
        var owner = Fixture.Employee();
        var manager = Fixture.Manager();
        var expense = Fixture.PendingExpense(owner);
        expense.Approve(manager, Money.Zero(Currency.EUR), MonthlyLimit, Fixture.UtcNow);

        var entry = ExpenseAuditEntry.Record(expense);

        Assert.Equal(ExpenseStatus.Approved, entry.Decision);
        Assert.Equal(expense.Id, entry.ExpenseId);
        Assert.Equal(owner.TenantId, entry.TenantId);
        Assert.Equal(owner.Id, entry.EmployeeId);
        Assert.Equal(manager.Id, entry.DecidedByEmployeeId);
        Assert.Equal(Fixture.UtcNow, entry.DecidedAt);
        Assert.Null(entry.Reason);
    }

    [Fact]
    public void Record_of_a_rejected_expense_captures_the_reason()
    {
        var manager = Fixture.Manager();
        var expense = Fixture.PendingExpense(Fixture.Employee());
        var reason = RejectionReason.Of("Receipt is missing from the submission.");
        expense.Reject(manager, reason, Fixture.UtcNow);

        var entry = ExpenseAuditEntry.Record(expense);

        Assert.Equal(ExpenseStatus.Rejected, entry.Decision);
        Assert.Equal(reason.Value, entry.Reason);
        Assert.Equal(manager.Id, entry.DecidedByEmployeeId);
    }

    [Fact]
    public void Record_of_a_pending_expense_is_refused()
    {
        var pending = Fixture.PendingExpense(Fixture.Employee());

        Assert.Throws<InvalidAuditEntryException>(() => ExpenseAuditEntry.Record(pending));
    }
}
