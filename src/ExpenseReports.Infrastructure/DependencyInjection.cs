using ExpenseReports.Application.Abstractions;
using ExpenseReports.Infrastructure.Persistence;
using ExpenseReports.Infrastructure.Persistence.Repositories;
using ExpenseReports.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ExpenseReports.Infrastructure;

// This is the only layer that knows about EF Core, BCrypt and JWT. Binding each
// application-defined interface to its concrete implementation here is what makes
// the Dependency Inversion Principle real: the inner layers depend on the
// abstractions (IExpenseRepository, IPasswordHasher...) and this method decides
// who fulfils them. Swapping Postgres or the hashing algorithm is a change here
// only, with no impact on the domain or the handlers.
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // AddDbContext registers the context as Scoped (one per request) — the
        // lifetime that makes the tenant filter safe: the tenant id is captured
        // once per request and cannot bleed across requests.
        services.AddDbContext<ExpenseReportsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Postgres")));

        // Repositories and the unit of work are Scoped because they share the
        // request's DbContext. Registered against their interfaces so the
        // application layer never sees the EF Core implementations.
        services.AddScoped<IExpenseRepository, ExpenseRepository>();
        services.AddScoped<IEmployeeRepository, EmployeeRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<DatabaseSeeder>();

        // Singletons: the hasher and token issuer are stateless and thread-safe,
        // so one shared instance for the whole app is correct and cheaper.
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        // Bind the Jwt configuration section to a typed options object and fail
        // fast at startup (ValidateOnStart) if it is missing or the signing key
        // is too weak — better a boot-time error than a broken token at runtime.
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
