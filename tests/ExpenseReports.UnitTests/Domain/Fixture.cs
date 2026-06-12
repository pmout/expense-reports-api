using ExpenseReports.Domain.Employees;
using ExpenseReports.Domain.Expenses;
using ExpenseReports.Domain.ValueObjects;

namespace ExpenseReports.UnitTests.Domain;

/// <summary>
/// Deterministic test data. A fixed "now" keeps date-window assertions stable.
/// </summary>
internal static class Fixture
{
    public static readonly DateTimeOffset UtcNow = new(2026, 06, 12, 10, 30, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = DateOnly.FromDateTime(UtcNow.UtcDateTime);
    public static readonly Guid TenantId = Guid.NewGuid();

    public static Employee Employee(Guid? tenantId = null, Role role = Role.Employee, string email = "alice@acme.test")
        => ExpenseReports.Domain.Employees.Employee.Create(
            tenantId ?? TenantId, "Alice Santos", Email.Of(email), role, "bcrypt-hash");

    public static Employee Manager(Guid? tenantId = null, string email = "boss@acme.test")
        => Employee(tenantId, Role.Manager, email);

    public static Expense PendingExpense(
        Employee owner,
        decimal amount = 100m,
        Currency currency = Currency.EUR,
        DateOnly? expenseDate = null)
        => Expense.Submit(
            owner,
            Money.Of(amount, currency),
            ExpenseCategory.Meal,
            "Team lunch with a client",
            expenseDate ?? Today,
            UtcNow);
}
