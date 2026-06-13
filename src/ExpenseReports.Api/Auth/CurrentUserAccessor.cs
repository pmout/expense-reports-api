using System.Security.Claims;
using ExpenseReports.Application.Abstractions;
using ExpenseReports.Domain.Employees;
using ExpenseReports.Infrastructure.Security;
using Microsoft.IdentityModel.JsonWebTokens;

namespace ExpenseReports.Api.Auth;

/// <summary>
/// Reads the authenticated identity from the validated JWT. The claims are the
/// single source of tenant/employee identity for the whole request — including
/// the persistence layer's tenant query filters.
/// </summary>
// Implements two interfaces deliberately: ICurrentUser (the rich identity the
// handlers need) and ITenantProvider (the nullable tenant the DbContext needs).
// One class, one source of truth — the value used to authorize an action and the
// value used to filter the SQL can never diverge.
internal sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUser, ITenantProvider
{
    // The ClaimsPrincipal populated by the JWT middleware for the current request.
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    // ICurrentUser is consumed only inside authenticated endpoints, so a missing
    // claim here is a programming error (an unauthenticated call slipped through),
    // hence it throws rather than returning a default.
    public Guid EmployeeId => RequiredGuidClaim(JwtRegisteredClaimNames.Sub);

    public Guid TenantId => RequiredGuidClaim(JwtClaims.TenantId);

    public Role Role =>
        Enum.TryParse<Role>(Principal?.FindFirstValue(JwtClaims.Role), out var role)
            ? role
            : throw new InvalidOperationException("Authenticated user has no valid role claim.");

    // Explicit interface implementation: ITenantProvider.TenantId is intentionally
    // *nullable* and must NOT throw. It runs for every query, including anonymous
    // ones (e.g. login), where no tenant exists — returning null makes the query
    // filter match nothing (fail closed) instead of blowing up.
    Guid? ITenantProvider.TenantId =>
        Guid.TryParse(Principal?.FindFirstValue(JwtClaims.TenantId), out var tenantId)
            ? tenantId
            : null;

    private Guid RequiredGuidClaim(string claimType) =>
        Guid.TryParse(Principal?.FindFirstValue(claimType), out var value)
            ? value
            : throw new InvalidOperationException($"Authenticated user has no valid '{claimType}' claim.");
}
