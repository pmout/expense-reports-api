using ExpenseReports.Domain.Common;

namespace ExpenseReports.Domain.Tenants;

/// <summary>
/// A client company. <see cref="MonthlyExpenseLimit"/> is the monthly cap of
/// approved expenses per employee, applied independently per currency (the
/// domain has no exchange rates, so amounts in different currencies are never
/// summed together).
/// </summary>
// A `class`, not a `record`: a Tenant is an entity defined by its identity (Id),
// not by its values. Two tenants named "Acme" are different companies. Sealed to
// keep the model closed — nothing should subclass a domain entity.
public sealed class Tenant
{
    // `= null!` tells the compiler "trust me, this is assigned" — true because
    // EF Core sets it when materializing and the Create factory sets it otherwise.
    // It silences the nullable warning without making the property actually nullable.
    public Guid Id { get; }
    public string Name { get; } = null!;
    public decimal MonthlyExpenseLimit { get; }

    // Parameterless constructor exists solely for EF Core's materialization; it
    // is private so application code cannot create an unvalidated tenant.
    private Tenant() { }

    private Tenant(Guid id, string name, decimal monthlyExpenseLimit)
    {
        Id = id;
        Name = name;
        MonthlyExpenseLimit = monthlyExpenseLimit;
    }

    // Static factory instead of a public constructor: it has a meaningful name,
    // validates its inputs, and is the only way to obtain a valid Tenant.
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
