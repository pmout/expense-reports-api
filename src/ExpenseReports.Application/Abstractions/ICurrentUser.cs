using ExpenseReports.Domain.Employees;

namespace ExpenseReports.Application.Abstractions;

/// <summary>
/// Identity of the authenticated employee, sourced from the JWT claims.
/// This is the only place tenant identity comes from — request payloads are
/// never trusted for tenant or employee ids.
/// </summary>
public interface ICurrentUser
{
    Guid TenantId { get; }
    Guid EmployeeId { get; }
    Role Role { get; }
}
