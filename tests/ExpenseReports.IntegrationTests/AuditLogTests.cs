using ExpenseReports.Domain.Auditing;
using ExpenseReports.Domain.Expenses;
using ExpenseReports.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseReports.IntegrationTests;

/// <summary>
/// The audit-log bonus: every approve/reject must leave one immutable row in the
/// separate audit table, written in the same transaction as the decision.
/// </summary>
public sealed class AuditLogTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Approving_writes_an_approved_audit_entry()
    {
        var employee = await ClientForAsync("alice@acme.test");
        var manager = await ClientForAsync("manager@acme.test");
        var expense = await SubmitExpenseAsync(employee);

        var response = await ApproveAsync(manager, expense.Id);
        response.EnsureSuccessStatusCode();

        var entry = await SingleAuditEntryForAsync(expense.Id);
        Assert.Equal(ExpenseStatus.Approved, entry.Decision);
        Assert.Null(entry.Reason);
    }

    [Fact]
    public async Task Rejecting_writes_a_rejected_audit_entry_with_the_reason()
    {
        var employee = await ClientForAsync("alice@acme.test");
        var manager = await ClientForAsync("manager@acme.test");
        var expense = await SubmitExpenseAsync(employee);

        var response = await RejectAsync(manager, expense.Id, "Receipt is missing from the submission.");
        response.EnsureSuccessStatusCode();

        var entry = await SingleAuditEntryForAsync(expense.Id);
        Assert.Equal(ExpenseStatus.Rejected, entry.Decision);
        Assert.Equal("Receipt is missing from the submission.", entry.Reason);
    }

    [Fact]
    public async Task A_pending_expense_has_no_audit_entry()
    {
        var employee = await ClientForAsync("bruno@acme.test");
        var expense = await SubmitExpenseAsync(employee);

        Assert.Empty(await AuditEntriesForAsync(expense.Id));
    }

    [Fact]
    public async Task Audit_entry_is_stamped_with_the_deciding_tenant()
    {
        var employee = await ClientForAsync("bruno@globex.test");
        var manager = await ClientForAsync("manager@globex.test");
        var expense = await SubmitExpenseAsync(employee, amount: 10m, currency: "BRL");
        (await ApproveAsync(manager, expense.Id)).EnsureSuccessStatusCode();

        var entry = await SingleAuditEntryForAsync(expense.Id);

        // The Globex tenant id; never Acme's. Confirms the audit row is tenant-scoped.
        var globexTenantId = await TenantIdOfAsync("manager@globex.test");
        Assert.Equal(globexTenantId, entry.TenantId);
    }

    // Reads bypass the tenant query filter (no HTTP request -> no tenant in scope),
    // exactly as the audit data is inspected from outside a request here.
    private async Task<IReadOnlyList<ExpenseAuditEntry>> AuditEntriesForAsync(Guid expenseId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpenseReportsDbContext>();
        return await db.AuditEntries.IgnoreQueryFilters()
            .Where(a => a.ExpenseId == expenseId)
            .ToListAsync();
    }

    private async Task<ExpenseAuditEntry> SingleAuditEntryForAsync(Guid expenseId)
    {
        var entries = await AuditEntriesForAsync(expenseId);
        return Assert.Single(entries);
    }

    private async Task<Guid> TenantIdOfAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpenseReportsDbContext>();
        var employee = await db.Employees.IgnoreQueryFilters()
            .FirstAsync(e => e.Email == ExpenseReports.Domain.ValueObjects.Email.Of(email));
        return employee.TenantId;
    }
}
