using FluentValidation;

namespace ExpenseReports.Api.Validation;

/// <summary>
/// Endpoint filter that validates the bound request body before the handler
/// runs, returning 400 Problem Details on failure.
/// </summary>
internal sealed class ValidationFilter<TRequest>(IValidator<TRequest> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var request = context.Arguments.OfType<TRequest>().FirstOrDefault();
        if (request is null)
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Missing request body");

        var result = await validator.ValidateAsync(request, context.HttpContext.RequestAborted);
        if (!result.IsValid)
            return Results.ValidationProblem(result.ToDictionary());

        return await next(context);
    }
}
