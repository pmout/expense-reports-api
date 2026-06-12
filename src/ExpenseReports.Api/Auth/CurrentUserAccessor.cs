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
internal sealed class CurrentUserAccessor(IHttpContextAccessor httpContextAccessor) : ICurrentUser, ITenantProvider
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public Guid EmployeeId => RequiredGuidClaim(JwtRegisteredClaimNames.Sub);

    public Guid TenantId => RequiredGuidClaim(JwtClaims.TenantId);

    public Role Role =>
        Enum.TryParse<Role>(Principal?.FindFirstValue(JwtClaims.Role), out var role)
            ? role
            : throw new InvalidOperationException("Authenticated user has no valid role claim.");

    Guid? ITenantProvider.TenantId =>
        Guid.TryParse(Principal?.FindFirstValue(JwtClaims.TenantId), out var tenantId)
            ? tenantId
            : null; // no authenticated tenant -> tenant-scoped queries match nothing

    private Guid RequiredGuidClaim(string claimType) =>
        Guid.TryParse(Principal?.FindFirstValue(claimType), out var value)
            ? value
            : throw new InvalidOperationException($"Authenticated user has no valid '{claimType}' claim.");
}
