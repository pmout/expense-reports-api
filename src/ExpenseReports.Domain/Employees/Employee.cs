using ExpenseReports.Domain.Common;
using ExpenseReports.Domain.ValueObjects;

namespace ExpenseReports.Domain.Employees;

/// <summary>
/// A person who belongs to exactly one tenant. Whether they may decide on
/// expenses depends on <see cref="Role"/>; see <see cref="Expenses.Expense"/>.
/// </summary>
public sealed class Employee
{
    public Guid Id { get; }
    public Guid TenantId { get; }
    public string Name { get; } = null!;
    public Email Email { get; } = null!;
    public Role Role { get; }

    /// <summary>
    /// Hash produced by the infrastructure layer (BCrypt). The domain never
    /// sees or stores a plaintext password.
    /// </summary>
    public string PasswordHash { get; } = null!;

    public bool IsManager => Role == Role.Manager;

    // For EF Core rehydration only; use the Create factory in application code.
    private Employee() { }

    private Employee(Guid id, Guid tenantId, string name, Email email, Role role, string passwordHash)
    {
        Id = id;
        TenantId = tenantId;
        Name = name;
        Email = email;
        Role = role;
        PasswordHash = passwordHash;
    }

    public static Employee Create(Guid tenantId, string name, Email email, Role role, string passwordHash)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
            throw new InvalidEmployeeException("Employee name is required.");
        if (tenantId == Guid.Empty)
            throw new InvalidEmployeeException("Employee must belong to a tenant.");
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new InvalidEmployeeException("Employee requires a password hash.");

        return new Employee(Guid.NewGuid(), tenantId, trimmed, email, role, passwordHash);
    }
}

public sealed class InvalidEmployeeException(string message) : DomainException(message);
