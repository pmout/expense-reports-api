using ExpenseReports.Application.Abstractions;
using ExpenseReports.Domain.Employees;
using ExpenseReports.Domain.Tenants;
using ExpenseReports.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ExpenseReports.Infrastructure.Persistence;

/// <summary>
/// Demo data: 2 tenants, each with 2 employees and 1 manager. Idempotent —
/// running it against a seeded database is a no-op. Credentials are listed in
/// the README; all seed users share the same demo password.
/// </summary>
public sealed class DatabaseSeeder(
    ExpenseReportsDbContext db,
    IPasswordHasher passwordHasher,
    ILogger<DatabaseSeeder> logger)
{
    public const string DemoPassword = "Passw0rd!demo";

    public async Task SeedAsync(CancellationToken ct = default)
    {
        // Idempotency guard: makes it safe to run on every startup (docker-compose
        // sets SeedOnStartup=true), so restarting the container never duplicates data.
        if (await db.Tenants.AnyAsync(ct))
        {
            logger.LogInformation("Database already seeded, skipping");
            return;
        }

        // Hash once and reuse: every demo user shares the same password, and BCrypt
        // is deliberately slow, so hashing per user would needlessly slow seeding.
        var passwordHash = passwordHasher.Hash(DemoPassword);

        // Two tenants with deliberately different limits (1000 vs 5000) so the
        // monthly-limit behavior is easy to demonstrate against either.
        AddTenant("Acme Corporation", 1000m, "acme.test", passwordHash);
        AddTenant("Globex Brasil", 5000m, "globex.test", passwordHash);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded 2 tenants with 3 employees each");
    }

    private void AddTenant(string name, decimal monthlyLimit, string emailDomain, string passwordHash)
    {
        var tenant = Tenant.Create(name, monthlyLimit);
        db.Tenants.Add(tenant);

        db.Employees.AddRange(
            Employee.Create(tenant.Id, "Alice", Email.Of($"alice@{emailDomain}"), Role.Employee, passwordHash),
            Employee.Create(tenant.Id, "Bruno", Email.Of($"bruno@{emailDomain}"), Role.Employee, passwordHash),
            Employee.Create(tenant.Id, "Marta", Email.Of($"manager@{emailDomain}"), Role.Manager, passwordHash));
    }
}
