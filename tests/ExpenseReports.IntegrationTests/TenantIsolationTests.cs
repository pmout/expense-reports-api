using System.Net;
using System.Net.Http.Json;
using ExpenseReports.Application.Common;
using ExpenseReports.Application.Expenses;

namespace ExpenseReports.IntegrationTests;

/// <summary>
/// System invariant: no data ever crosses tenant boundaries. These tests drive
/// the real HTTP + SQL pipeline with users from both seeded tenants.
/// </summary>
public sealed class TenantIsolationTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Tenant_B_users_cannot_read_a_tenant_A_expense()
    {
        var acmeEmployee = await ClientForAsync("alice@acme.test");
        var expense = await SubmitExpenseAsync(acmeEmployee);

        var globexManager = await ClientForAsync("manager@globex.test");
        var asManager = await globexManager.GetAsync($"/expenses/{expense.Id}");

        var globexEmployee = await ClientForAsync("alice@globex.test");
        var asEmployee = await globexEmployee.GetAsync($"/expenses/{expense.Id}");

        // 404, not 403: confirming the expense exists would already leak data.
        Assert.Equal(HttpStatusCode.NotFound, asManager.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, asEmployee.StatusCode);

        var owner = await acmeEmployee.GetAsync($"/expenses/{expense.Id}");
        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);
    }

    [Fact]
    public async Task Tenant_B_manager_cannot_approve_or_reject_a_tenant_A_expense()
    {
        var acmeEmployee = await ClientForAsync("alice@acme.test");
        var expense = await SubmitExpenseAsync(acmeEmployee);

        var globexManager = await ClientForAsync("manager@globex.test");

        var approveAttempt = await ApproveAsync(globexManager, expense.Id);
        var rejectAttempt = await RejectAsync(globexManager, expense.Id, "Not an expense of my tenant.");

        Assert.Equal(HttpStatusCode.NotFound, approveAttempt.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, rejectAttempt.StatusCode);

        // The expense is untouched and still approvable by the right manager.
        var acmeManager = await ClientForAsync("manager@acme.test");
        var legitimateApproval = await ApproveAsync(acmeManager, expense.Id);
        Assert.Equal(HttpStatusCode.OK, legitimateApproval.StatusCode);
    }

    [Fact]
    public async Task Pending_list_never_contains_other_tenants_expenses()
    {
        var acmeEmployee = await ClientForAsync("bruno@acme.test");
        var globexEmployee = await ClientForAsync("bruno@globex.test");
        var acmeExpense = await SubmitExpenseAsync(acmeEmployee);
        var globexExpense = await SubmitExpenseAsync(globexEmployee);

        var globexManager = await ClientForAsync("manager@globex.test");
        var page = await globexManager.GetFromJsonAsync<Page<ExpenseResponse>>("/expenses/pending?pageSize=100");

        Assert.Contains(page!.Items, e => e.Id == globexExpense.Id);
        Assert.DoesNotContain(page.Items, e => e.Id == acmeExpense.Id);
    }
}
