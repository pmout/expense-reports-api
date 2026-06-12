using ExpenseReports.Application.Auth;
using ExpenseReports.Application.Expenses;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseReports.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);

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
