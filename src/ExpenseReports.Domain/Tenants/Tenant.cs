using ExpenseReports.Domain.Common;

namespace ExpenseReports.Domain.Tenants;

/// <summary>
/// A client company. <see cref="MonthlyExpenseLimit"/> is the monthly cap of
/// approved expenses per employee, applied independently per currency (the
/// domain has no exchange rates, so amounts in different currencies are never
/// summed together).
/// </summary>
public sealed class Tenant
{
    public Guid Id { get; }
    public string Name { get; } = null!;
    public decimal MonthlyExpenseLimit { get; }

    private Tenant() { } // EF Core

    private Tenant(Guid id, string name, decimal monthlyExpenseLimit)
    {
        Id = id;
        Name = name;
        MonthlyExpenseLimit = monthlyExpenseLimit;
    }

    public static Tenant Create(string name, decimal monthlyExpenseLimit)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            throw new InvalidTenantException("Tenant name is required.");
        if (monthlyExpenseLimit <= 0)
            throw new InvalidTenantException("Monthly expense limit must be greater than zero.");

        return new Tenant(Guid.NewGuid(), trimmed, monthlyExpenseLimit);
    }
}

public sealed class InvalidTenantException(string message) : DomainException(message);
