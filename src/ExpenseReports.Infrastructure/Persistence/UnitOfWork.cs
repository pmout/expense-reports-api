using ExpenseReports.Application.Abstractions;

namespace ExpenseReports.Infrastructure.Persistence;

/// <summary>
/// Thin wrapper over the DbContext so the application layer can commit changes
/// and run a unit of work inside a transaction without depending on EF Core.
/// </summary>
internal sealed class UnitOfWork(ExpenseReportsDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    /// <summary>
    /// Runs <paramref name="action"/> in a single database transaction, used by
    /// approval so the advisory lock, the total query and the save all share one
    /// transaction and are released together on commit or rollback.
    /// </summary>
    public async Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var result = await action(ct);
        await transaction.CommitAsync(ct);
        return result;
    }
}
