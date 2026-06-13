using ExpenseReports.Application.Auth;
using ExpenseReports.Application.Expenses;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseReports.Application;

// `static` because this class is never instantiated: it is only a container for
// the AddApplication extension method. Each layer owns one of these, so the
// composition root (Program.cs) wires the whole app with one call per layer
// (AddApplication + AddInfrastructure) and never references concrete types
// directly — that keeps the dependency direction pointing inward.
public static class DependencyInjection
{
    // Extension method on IServiceCollection (the `this` parameter) so the call
    // reads fluently as builder.Services.AddApplication(). Returning the same
    // collection allows further chaining.
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // TimeProvider is the .NET abstraction over the clock. Registered as a
        // singleton (it is stateless) and injected everywhere instead of calling
        // DateTime.UtcNow, which makes time deterministic and testable.
        services.AddSingleton(TimeProvider.System);

        // Handlers are Scoped: one instance per HTTP request, matching the
        // lifetime of the DbContext they ultimately use. They hold no state
        // between requests, so a wider lifetime would be wrong.
        services.AddScoped<LoginHandler>();
        services.AddScoped<SubmitExpenseHandler>();
        services.AddScoped<ApproveExpenseHandler>();
        services.AddScoped<RejectExpenseHandler>();
        services.AddScoped<GetExpenseHandler>();
        services.AddScoped<ListMyExpensesHandler>();
        services.AddScoped<ListPendingExpensesHandler>();

        return services;
    }
}
