using ExpenseReports.Application.Common;
using ExpenseReports.Domain.Employees;
using ExpenseReports.Domain.Expenses;
using ExpenseReports.Domain.Tenants;
using ExpenseReports.Domain.ValueObjects;

namespace ExpenseReports.Application.Abstractions;

/// <summary>
/// All read methods are tenant-scoped at the query level: an implementation
/// must make it impossible to load another tenant's data through them.
/// </summary>
public interface IExpenseRepository
{
    Task AddAsync(Expense expense, CancellationToken ct);
    Task<Expense?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Page<Expense>> ListByEmployeeAsync(Guid employeeId, PageRequest page, CancellationToken ct);
    Task<Page<Expense>> ListPendingAsync(PageRequest page, CancellationToken ct);

    /// <summary>
    /// Sum of an employee's approved expenses in the given month, for one currency.
    /// </summary>
    Task<Money> GetApprovedTotalAsync(Guid employeeId, Currency currency, int year, int month, CancellationToken ct);

    /// <summary>
    /// Serializes concurrent approvals for the same employee within the current
    /// transaction, so the monthly-limit check cannot race.
    /// </summary>
    Task LockEmployeeForApprovalAsync(Guid employeeId, CancellationToken ct);
}

public interface IEmployeeRepository
{
    Task<Employee?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Lookup for the login flow only. Runs before any tenant is known, so it is
    /// the single deliberately tenant-unfiltered query in the system.
    /// </summary>
    Task<Employee?> FindForAuthenticationAsync(Email email, CancellationToken ct);
}

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct);
}

public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
}
