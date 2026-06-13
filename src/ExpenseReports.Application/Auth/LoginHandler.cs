using ExpenseReports.Application.Abstractions;
using ExpenseReports.Application.Common;
using ExpenseReports.Domain.ValueObjects;

namespace ExpenseReports.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt);

/// <summary>
/// Authenticates an employee and issues a JWT. Every failure path returns the
/// same <see cref="InvalidCredentialsException"/> so a caller cannot tell a
/// bad password from an unknown account (no account enumeration).
/// </summary>
public sealed class LoginHandler(
    IEmployeeRepository employees,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer)
{
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        // A malformed e-mail is just a failed login, not a 400 — same response
        // as any other bad credential.
        Email email;
        try
        {
            email = Email.Of(request.Email);
        }
        catch (InvalidEmailException)
        {
            throw new InvalidCredentialsException();
        }

        // BCrypt.Verify is always run only when the employee exists; the uniform
        // exception below hides whether it was the e-mail or the password at fault.
        var employee = await employees.FindForAuthenticationAsync(email, ct);
        if (employee is null || !passwordHasher.Verify(request.Password, employee.PasswordHash))
            throw new InvalidCredentialsException();

        var token = tokenIssuer.Issue(employee);
        return new LoginResponse(token.Token, token.ExpiresAt);
    }
}
