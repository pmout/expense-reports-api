using ExpenseReports.Application.Abstractions;
using ExpenseReports.Domain.Employees;
using ExpenseReports.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ExpenseReports.Infrastructure.Persistence.Repositories;

// `internal`: nothing outside this assembly should reference the concrete
// repository — callers depend on IEmployeeRepository, and only the DI
// registration in this same assembly binds the two. The primary constructor
// `(ExpenseReportsDbContext db)` injects the request's DbContext.
internal sealed class EmployeeRepository(ExpenseReportsDbContext db) : IEmployeeRepository
{
    public Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<Employee?> FindForAuthenticationAsync(Email email, CancellationToken ct) =>
        // Deliberately unfiltered: login happens before a tenant is known.
        // This is the only IgnoreQueryFilters in the codebase.
        db.Employees.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Email == email, ct);
}
