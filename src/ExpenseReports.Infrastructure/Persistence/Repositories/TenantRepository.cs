using ExpenseReports.Application.Abstractions;
using ExpenseReports.Domain.Tenants;
using Microsoft.EntityFrameworkCore;

namespace ExpenseReports.Infrastructure.Persistence.Repositories;

internal sealed class TenantRepository(ExpenseReportsDbContext db) : ITenantRepository
{
    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);
}
