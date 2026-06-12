using ExpenseReports.Domain.Employees;

namespace ExpenseReports.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}

public interface ITokenIssuer
{
    AccessToken Issue(Employee employee);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
