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

    private static (int Status, string Title, string? Detail) Map(Exception exception) => exception switch
    {
        // A decision raced with another one: the expense is no longer Pending.
        ExpenseAlreadyDecidedException already =>
            (StatusCodes.Status409Conflict, "Expense already decided", already.Message),
        DbUpdateConcurrencyException =>
            (StatusCodes.Status409Conflict, "Concurrent update",
                "The expense was modified by another request. Fetch it again to see its current state."),
        DomainException domain =>
            (StatusCodes.Status422UnprocessableEntity, "Business rule violation", domain.Message),
        NotFoundException notFound =>
            (StatusCodes.Status404NotFound, "Resource not found", notFound.Message),
        InvalidCredentialsException =>
            (StatusCodes.Status401Unauthorized, "Authentication failed", "Invalid e-mail or password."),
        BadHttpRequestException badRequest =>
            (StatusCodes.Status400BadRequest, "Invalid request", badRequest.Message),
        _ =>
            (StatusCodes.Status500InternalServerError, "An unexpected error occurred", null)
    };
}
