using ExpenseReports.Application.Abstractions;
using ExpenseReports.Application.Common;
using ExpenseReports.Domain.ValueObjects;

namespace ExpenseReports.Application.Auth;

public sealed record LoginRequest(string Email, string Password);

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt);

public sealed class LoginHandler(
    IEmployeeRepository employees,
    IPasswordHasher passwordHasher,
    ITokenIssuer tokenIssuer)
{
    public async Task<LoginResponse> HandleAsync(LoginRequest request, CancellationToken ct)
    {
        Email email;
        try
        {
            email = Email.Of(request.Email);
        }
        catch (InvalidEmailException)
        {
            throw new InvalidCredentialsException();
        }

        var employee = await employees.FindForAuthenticationAsync(email, ct);
        if (employee is null || !passwordHasher.Verify(request.Password, employee.PasswordHash))
            throw new InvalidCredentialsException();

        var token = tokenIssuer.Issue(employee);
        return new LoginResponse(token.Token, token.ExpiresAt);
    }
}
