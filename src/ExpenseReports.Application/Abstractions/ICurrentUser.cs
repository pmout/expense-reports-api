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

// Kept as a separate, single-member interface (Interface Segregation) so the
// DbContext depends only on the nullable tenant it needs, not the full identity.
// It also avoids a layering problem: the DbContext lives in Infrastructure and a
// non-null guarantee belongs to authenticated requests, which it cannot assume.
/// <summary>
/// Nullable view of the current tenant, consumed by the persistence layer's
/// global query filters. Null (no authenticated user) means queries match
/// nothing — fail closed, never open.
/// </summary>
public interface ITenantProvider
{
    Guid? TenantId { get; }
}
