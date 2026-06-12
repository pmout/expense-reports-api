using System.Net;
using System.Net.Http.Json;
using ExpenseReports.Application.Common;
using ExpenseReports.Application.Expenses;

namespace ExpenseReports.IntegrationTests;

public sealed class ExpenseLifecycleTests(ApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Submitted_expense_is_pending_and_listed_for_its_owner()
    {
        var employee = await ClientForAsync("alice@acme.test");

        var expense = await SubmitExpenseAsync(employee);

        Assert.Equal("Pending", expense.Status);
        var page = await employee.GetFromJsonAsync<Page<ExpenseResponse>>("/expenses?pageSize=100");
        Assert.Contains(page!.Items, e => e.Id == expense.Id);
    }

    [Fact]
    public async Task Employees_only_see_their_own_expenses_in_the_listing()
    {
        var alice = await ClientForAsync("alice@acme.test");
        var bruno = await ClientForAsync("bruno@acme.test");
        var aliceExpense = await SubmitExpenseAsync(alice);

        var brunoPage = await bruno.GetFromJsonAsync<Page<ExpenseResponse>>("/expenses?pageSize=100");

        Assert.DoesNotContain(brunoPage!.Items, e => e.Id == aliceExpense.Id);
        // Same tenant, but not the owner nor a manager: detail is hidden too.
        var detail = await bruno.GetAsync($"/expenses/{aliceExpense.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
    }

    [Fact]
    public async Task Manager_approves_a_pending_expense()
    {
        var employee = await ClientForAsync("alice@acme.test");
        var manager = await ClientForAsync("manager@acme.test");
        var expense = await SubmitExpenseAsync(employee);

        var response = await ApproveAsync(manager, expense.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var approved = await response.Content.ReadFromJsonAsync<ExpenseResponse>();
        Assert.Equal("Approved", approved!.Status);
        Assert.NotNull(approved.DecidedAt);
    }

    [Fact]
    public async Task Decided_expense_cannot_be_decided_again()
    {
        var employee = await ClientForAsync("alice@acme.test");
        var manager = await ClientForAsync("manager@acme.test");
        var expense = await SubmitExpenseAsync(employee);
        await ApproveAsync(manager, expense.Id);

        var secondDecision = await RejectAsync(manager, expense.Id, "Trying to undo the approval.");

        Assert.Equal(HttpStatusCode.Conflict, secondDecision.StatusCode);
    }

    [Fact]
    public async Task Non_manager_cannot_approve()
    {
        var alice = await ClientForAsync("alice@acme.test");
        var bruno = await ClientForAsync("bruno@acme.test");
        var expense = await SubmitExpenseAsync(alice);

        var response = await ApproveAsync(bruno, expense.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Manager_cannot_approve_own_expense()
    {
        var manager = await ClientForAsync("manager@acme.test");
        var ownExpense = await SubmitExpenseAsync(manager);

        var response = await ApproveAsync(manager, ownExpense.Id);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Rejection_requires_a_reason_of_at_least_10_characters()
    {
        var employee = await ClientForAsync("alice@acme.test");
        var manager = await ClientForAsync("manager@acme.test");
        var expense = await SubmitExpenseAsync(employee);

        var tooShort = await RejectAsync(manager, expense.Id, "too short");
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);

        var valid = await RejectAsync(manager, expense.Id, "Receipt is missing from the submission.");
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode);
        var rejected = await valid.Content.ReadFromJsonAsync<ExpenseResponse>();
        Assert.Equal("Rejected", rejected!.Status);
        Assert.NotNull(rejected.RejectionReason);
    }

    [Fact]
    public async Task Approval_beyond_the_monthly_limit_is_refused_with_422()
    {
        // globex monthly limit is 5000. BRL keeps this scenario isolated from
        // every other test (the limit applies per currency).
        var employee = await ClientForAsync("bruno@globex.test");
        var manager = await ClientForAsync("manager@globex.test");
        var first = await SubmitExpenseAsync(employee, amount: 3000m, currency: "BRL");
        var second = await SubmitExpenseAsync(employee, amount: 2500m, currency: "BRL");

        var firstApproval = await ApproveAsync(manager, first.Id);
        Assert.Equal(HttpStatusCode.OK, firstApproval.StatusCode);

        var secondApproval = await ApproveAsync(manager, second.Id);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondApproval.StatusCode);
        var problem = await secondApproval.Content.ReadAsStringAsync();
        Assert.Contains("monthly limit", problem);

        var stillPending = await employee.GetFromJsonAsync<ExpenseResponse>($"/expenses/{second.Id}");
        Assert.Equal("Pending", stillPending!.Status);
    }

    [Fact]
    public async Task Invalid_payload_is_rejected_at_the_edge_with_400()
    {
        var employee = await ClientForAsync("alice@acme.test");

        var response = await employee.PostAsJsonAsync("/expenses", new
        {
            amount = -10m,
            currency = "EUR",
            category = "Meal",
            description = "bad",
            expenseDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
