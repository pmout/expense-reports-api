using ExpenseReports.Domain.Common;
using ExpenseReports.Domain.Expenses;

namespace ExpenseReports.Domain.Auditing;

/// <summary>
/// An immutable record of a single approve/reject decision, written to a
/// separate table. It is a snapshot taken at decision time: it never changes
/// afterwards, so it survives even if the expense is later corrected by a fix-up.
/// </summary>
// Modelled as an entity (it has its own identity and lifetime, independent of the
// expense). Built only through Record, which reads an already-decided expense —
// so an audit row can never describe a decision that did not happen.
public sealed class ExpenseAuditEntry
{
    public Guid Id { get; }
    public Guid TenantId { get; }
    public Guid ExpenseId { get; }
    public Guid EmployeeId { get; }
    public ExpenseStatus Decision { get; }
    public Guid DecidedByEmployeeId { get; }
    public DateTimeOffset DecidedAt { get; }

    /// <summary>The rejection reason, captured only for rejections.</summary>
    public string? Reason { get; }

    private ExpenseAuditEntry() { } // EF Core

    private ExpenseAuditEntry(
        Guid id, Guid tenantId, Guid expenseId, Guid employeeId,
        ExpenseStatus decision, Guid decidedByEmployeeId, DateTimeOffset decidedAt, string? reason)
    {
        Id = id;
        TenantId = tenantId;
        ExpenseId = expenseId;
        EmployeeId = employeeId;
        Decision = decision;
        DecidedByEmployeeId = decidedByEmployeeId;
        DecidedAt = decidedAt;
        Reason = reason;
    }

    /// <summary>
    /// Captures the decision held by an already-approved/rejected expense. Throws
    /// if the expense is still Pending — there is no decision to audit yet.
    /// </summary>
    public static ExpenseAuditEntry Record(Expense expense)
    {
        if (expense.Status is not (ExpenseStatus.Approved or ExpenseStatus.Rejected))
            throw new InvalidAuditEntryException("Only a decided expense can be audited.");

        // DecidedAt / DecidedByEmployeeId are always set once an expense is
        // decided; the null-forgiving operator reflects that guaranteed invariant.
        return new ExpenseAuditEntry(
            Guid.NewGuid(),
            expense.TenantId,
            expense.Id,
            expense.EmployeeId,
            expense.Status,
            expense.DecidedByEmployeeId!.Value,
            expense.DecidedAt!.Value,
            expense.RejectionReason?.Value);
    }
}

public sealed class InvalidAuditEntryException(string message) : DomainException(message);
