using ExpenseReports.Application.Abstractions;
using ExpenseReports.Infrastructure.Persistence;
using ExpenseReports.Infrastructure.Persistence.Repositories;
using ExpenseReports.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseReports.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ExpenseReportsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<DatabaseSeeder>();

        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .Validate(
                o => o.SigningKey.Length >= 32 && o.Issuer.Length > 0 && o.Audience.Length > 0,
                "Jwt options require Issuer, Audience and a SigningKey of at least 32 characters.")
            .ValidateOnStart();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();

        return services;
    }
}
