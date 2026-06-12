using ExpenseReports.Application.Abstractions;

namespace ExpenseReports.Infrastructure.Persistence;

internal sealed class UnitOfWork(ExpenseReportsDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await action(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
