using ExpenseReports.Api.Validation;
using ExpenseReports.Application.Auth;

namespace ExpenseReports.Api.Endpoints;

internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/auth/login",
                async (LoginRequest request, LoginHandler handler, CancellationToken ct) =>
                    Results.Ok(await handler.HandleAsync(request, ct)))
            .AddEndpointFilter<ValidationFilter<LoginRequest>>()
            .RequireRateLimiting("login")
            .AllowAnonymous()
            .WithName("Login")
            .WithSummary("Authenticates an employee and returns a JWT")
            .Produces<LoginResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized);
    }
}
