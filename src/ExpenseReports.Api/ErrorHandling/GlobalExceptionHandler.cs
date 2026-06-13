using ExpenseReports.Application.Common;
using ExpenseReports.Domain.Common;
using ExpenseReports.Domain.Expenses;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ExpenseReports.Api.ErrorHandling;

/// <summary>
/// Single place where exceptions become Problem Details responses. Business
/// errors keep their message; unexpected errors are logged and answered with a
/// generic 500 — no stack traces or internals ever reach the client.
/// </summary>
internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = Map(exception);

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception processing {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }

    // Translates each exception type to the HTTP status that best fits its meaning.
    // Order matters: the more specific types are matched before the broader
    // DomainException they inherit from. The discard arm guarantees an unknown
    // exception never escapes as a stack trace — it becomes a generic 500.
    private static (int Status, string Title, string? Detail) Map(Exception exception) => exception switch
    {
        // 409 (not 422): the request was valid, but a concurrent decision already
        // moved the expense out of Pending — a conflict of state, not a bad rule.
        ExpenseAlreadyDecidedException already =>
            (StatusCodes.Status409Conflict, "Expense already decided", already.Message),
        // 409: optimistic concurrency (xmin) detected a competing write at commit.
        DbUpdateConcurrencyException =>
            (StatusCodes.Status409Conflict, "Concurrent update",
                "The expense was modified by another request. Fetch it again to see its current state."),
        // 422: the input was well-formed but breaks a business rule (limit, self-
        // approval, wrong tenant...). The domain's own message is safe to surface.
        DomainException domain =>
            (StatusCodes.Status422UnprocessableEntity, "Business rule violation", domain.Message),
        // 404: also returned for cross-tenant access, so existence is never revealed.
        NotFoundException notFound =>
            (StatusCodes.Status404NotFound, "Resource not found", notFound.Message),
        // 401 with a fixed message — never disclose whether it was the e-mail or password.
        InvalidCredentialsException =>
            (StatusCodes.Status401Unauthorized, "Authentication failed", "Invalid e-mail or password."),
        // 400: malformed request the model binder rejected before a handler ran.
        BadHttpRequestException badRequest =>
            (StatusCodes.Status400BadRequest, "Invalid request", badRequest.Message),
        // Anything unanticipated: 500 with no detail. The real exception is logged
        // (above), not returned, so internals never reach the client.
        _ =>
            (StatusCodes.Status500InternalServerError, "An unexpected error occurred", null)
    };
}
