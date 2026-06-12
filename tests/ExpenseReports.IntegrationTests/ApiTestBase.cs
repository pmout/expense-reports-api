using System.Net.Http.Headers;
using System.Net.Http.Json;
using ExpenseReports.Application.Abstractions;
using ExpenseReports.Application.Expenses;
using ExpenseReports.Domain.ValueObjects;
using ExpenseReports.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseReports.IntegrationTests;

/// <summary>
/// Seeded identities (see DatabaseSeeder): two tenants — acme.test (monthly
/// limit 1000) and globex.test (monthly limit 5000) — each with employees
/// alice@/bruno@ and a manager@.
/// </summary>
[Collection(ApiCollection.Name)]
public abstract class ApiTestBase(ApiFactory factory)
{
    protected ApiFactory Factory { get; } = factory;

    protected HttpClient AnonymousClient() => Factory.CreateClient();

    /// <summary>
    /// Issues a JWT directly through ITokenIssuer: tests of business behavior
    /// should not depend on (or consume budget from) the login endpoint.
    /// </summary>
    protected async Task<HttpClient> ClientForAsync(string email)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ExpenseReportsDbContext>();

        // Unfiltered on purpose: the test impersonates users of several tenants.
        var employee = await db.Employees.IgnoreQueryFilters()
            .FirstAsync(e => e.Email == Email.Of(email));

        var token = scope.ServiceProvider.GetRequiredService<ITokenIssuer>().Issue(employee);

        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return client;
    }

    protected static async Task<ExpenseResponse> SubmitExpenseAsync(
        HttpClient client, decimal amount = 25m, string currency = "EUR", string description = "Team lunch with a client")
    {
        var response = await client.PostAsJsonAsync("/expenses", new
        {
            amount,
            currency,
            category = "Meal",
            description,
            expenseDate = DateOnly.FromDateTime(DateTime.UtcNow)
        });

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ExpenseResponse>())!;
    }

    protected static Task<HttpResponseMessage> ApproveAsync(HttpClient client, Guid expenseId) =>
        client.PostAsync($"/expenses/{expenseId}/approve", content: null);

    protected static Task<HttpResponseMessage> RejectAsync(HttpClient client, Guid expenseId, string reason) =>
        client.PostAsJsonAsync($"/expenses/{expenseId}/reject", new { reason });
}
