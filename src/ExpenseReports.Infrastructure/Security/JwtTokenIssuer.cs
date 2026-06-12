using System.Text;
using ExpenseReports.Application.Abstractions;
using ExpenseReports.Domain.Employees;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ExpenseReports.Infrastructure.Security;

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
public static class JwtClaims
{
    public const string TenantId = "tenant_id";
    public const string Role = "role";
}

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
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = employee.Id.ToString(),
                [JwtClaims.TenantId] = employee.TenantId.ToString(),
                [JwtClaims.Role] = employee.Role.ToString()
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, expiresAt);
    }
}
