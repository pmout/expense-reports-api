using ExpenseReports.Application.Abstractions;
using ExpenseReports.Domain.Employees;
using ExpenseReports.Domain.Expenses;
using ExpenseReports.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace ExpenseReports.Infrastructure.Persistence;

public sealed class ExpenseReportsDbContext : DbContext
{
    // Captured once per context instance (one instance per request). Global
    // query filters compare against it, so every query is tenant-scoped in SQL.
    private readonly Guid? _currentTenantId;

    public ExpenseReportsDbContext(DbContextOptions<ExpenseReportsDbContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        _currentTenantId = tenantProvider.TenantId;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<Expense> Expenses => Set<Expense>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ExpenseReportsDbContext).Assembly);

        modelBuilder.Entity<Employee>().HasQueryFilter(e => e.TenantId == _currentTenantId);
        modelBuilder.Entity<Expense>().HasQueryFilter(e => e.TenantId == _currentTenantId);
    }
}
