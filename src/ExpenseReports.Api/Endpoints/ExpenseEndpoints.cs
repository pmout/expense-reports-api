using ExpenseReports.Api.Validation;
using ExpenseReports.Application.Common;
using ExpenseReports.Application.Expenses;

namespace ExpenseReports.Api.Endpoints;

internal static class ExpenseEndpoints
{
    public const string ManagerPolicy = "ManagerOnly";

    public static void MapExpenseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/expenses")
            .RequireAuthorization()
            .WithTags("Expenses");

        group.MapPost("/",
                async (SubmitExpenseRequest request, SubmitExpenseHandler handler, CancellationToken ct) =>
                {
                    var expense = await handler.HandleAsync(request, ct);
                    return Results.Created($"/expenses/{expense.Id}", expense);
                })
            .AddEndpointFilter<ValidationFilter<SubmitExpenseRequest>>()
            .WithSummary("Submits an expense for approval")
            .Produces<ExpenseResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapGet("/",
                async (ListMyExpensesHandler handler, CancellationToken ct, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
                    Results.Ok(await handler.HandleAsync(new PageRequest(page, pageSize), ct)))
            .WithSummary("Lists the caller's own expenses (paginated)")
            .Produces<Page<ExpenseResponse>>();

        group.MapGet("/pending",
                async (ListPendingExpensesHandler handler, CancellationToken ct, int page = 1, int pageSize = PageRequest.DefaultPageSize) =>
                    Results.Ok(await handler.HandleAsync(new PageRequest(page, pageSize), ct)))
            .RequireAuthorization(ManagerPolicy)
            .WithSummary("Lists the tenant's pending expenses (managers only)")
            .Produces<Page<ExpenseResponse>>()
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/{id:guid}",
                async (Guid id, GetExpenseHandler handler, CancellationToken ct) =>
                    Results.Ok(await handler.HandleAsync(id, ct)))
            .WithSummary("Returns one expense; owners see their own, managers see the tenant's")
            .Produces<ExpenseResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/approve",
                async (Guid id, ApproveExpenseHandler handler, CancellationToken ct) =>
                    Results.Ok(await handler.HandleAsync(id, ct)))
            .RequireAuthorization(ManagerPolicy)
            .WithSummary("Approves a pending expense (managers only)")
            .Produces<ExpenseResponse>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        group.MapPost("/{id:guid}/reject",
                async (Guid id, RejectExpenseRequest request, RejectExpenseHandler handler, CancellationToken ct) =>
                    Results.Ok(await handler.HandleAsync(id, request, ct)))
            .RequireAuthorization(ManagerPolicy)
            .AddEndpointFilter<ValidationFilter<RejectExpenseRequest>>()
            .WithSummary("Rejects a pending expense with a reason (managers only)")
            .Produces<ExpenseResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);
    }
}
