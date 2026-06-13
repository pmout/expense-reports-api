using System.Text;
using ExpenseReports.Application.Abstractions;
using ExpenseReports.Domain.Employees;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ExpenseReports.Infrastructure.Security;

// Strongly-typed settings bound from the "Jwt" configuration section. `init`-only
// properties make it effectively immutable once bound. The empty-string defaults
// exist only so binding never produces nulls; real values are validated at startup.
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "";
    public string Audience { get; init; } = "";
    public string SigningKey { get; init; } = "";
    public int ExpiryMinutes { get; init; } = 60;
}

/// <summary>
/// Claim names shared between token issuing and token consumption.
/// </summary>
// Centralizing the strings means the issuer and the reader (CurrentUserAccessor,
// Program.cs) can never disagree on a claim name — a typo would be a compile error.
public static class JwtClaims
{
    public const string TenantId = "tenant_id";
    public const string Role = "role";
}

// `internal`: the rest of the app depends on ITokenIssuer, not this type. Takes
// the same injected TimeProvider as everything else, so issued/expiry times are
// consistent and testable.
internal sealed class JwtTokenIssuer(IOptions<JwtOptions> options, TimeProvider clock) : ITokenIssuer
{
    private readonly JwtOptions _options = options.Value;

    public AccessToken Issue(Employee employee)
    {
        var now = clock.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.ExpiryMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            // The claims the spec requires: sub (the employee), tenant_id (drives
            // all tenant isolation downstream) and role (gates manager actions).
            // Only identifiers go in — never the name, e-mail or anything sensitive,
            // because a JWT payload is signed, not encrypted, and is readable by anyone.
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = employee.Id.ToString(),
                [JwtClaims.TenantId] = employee.TenantId.ToString(),
                [JwtClaims.Role] = employee.Role.ToString()
            },
            // HMAC-SHA256 with the shared secret: the same key signs here and
            // validates in Program.cs. Symmetric is appropriate because the same
            // service issues and verifies; a multi-service setup would use RSA/ECDSA.
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, expiresAt);
    }
}
