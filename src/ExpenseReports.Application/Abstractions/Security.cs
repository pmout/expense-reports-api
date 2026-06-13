using ExpenseReports.Domain.Employees;

namespace ExpenseReports.Application.Abstractions;

// Security ports. The application orchestrates hashing and token issuing through
// these interfaces but stays ignorant of *how* — BCrypt and JWT are infrastructure
// details. That keeps the algorithms swappable and the handlers unit-testable.

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public interface ITokenIssuer
{
    AccessToken Issue(Employee employee);
}

// Carries the token and its expiry together, so the API can return ExpiresAt
// without re-parsing the token to find out when it lapses.
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
