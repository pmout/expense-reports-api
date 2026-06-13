using FluentValidation;

namespace ExpenseReports.Api.Validation;

/// <summary>
/// Endpoint filter that validates the bound request body before the handler
/// runs, returning 400 Problem Details on failure.
/// </summary>
// Generic over TRequest so a single filter validates any request type; the right
// IValidator&lt;TRequest&gt; is resolved by DI. Doing this as a filter keeps the
// endpoints free of validation plumbing and guarantees no handler ever runs on
// invalid input. The validator is required (constructor-injected), so a missing
// one fails fast rather than silently skipping validation.
internal sealed class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Pull the typed body out of the endpoint's arguments; absent means the
        // client sent nothing/garbage, which is itself a 400.
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Missing request body");

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (!result.IsValid)
            // ValidationProblem emits RFC 7807 with a per-field error dictionary.
            return Results.ValidationProblem(result.ToDictionary());

        // Valid: hand control to the next filter or the endpoint itself.
        return await next(context);
    }
}
