using ExpenseReports.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExpenseReports.Infrastructure.Persistence;

/// <summary>
/// Used only by `dotnet ef` at design time to generate migrations; never runs
/// in the application.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ExpenseReportsDbContext>
{
    public ExpenseReportsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ExpenseReportsDbContext>()
            .UseNpgsql("Host=localhost;Database=expense_reports;Username=postgres;Password=postgres")
            .Options;

        return new ExpenseReportsDbContext(options, new NoTenantProvider());
    }

    private sealed class NoTenantProvider : ITenantProvider
    {
        public Guid? TenantId => null;
    }
}
