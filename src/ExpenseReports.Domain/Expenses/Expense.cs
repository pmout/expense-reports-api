using ExpenseReports.Domain.Common;
using ExpenseReports.Domain.Employees;
using ExpenseReports.Domain.ValueObjects;

namespace ExpenseReports.Domain.Expenses;

/// <summary>
/// An expense submitted for reimbursement. All state transitions go through
/// <see cref="Approve"/> / <see cref="Reject"/>, which enforce the business
/// invariants — there is no way to build or mutate an expense into an invalid state.
/// </summary>
// This is the aggregate root: the consistency boundary for an expense. It is a
// `class` (an entity with identity), not a `record`, and it guards its own state
// — behavior lives here, not in the handlers, which is the core of a rich domain
// model. Constants are exposed so the API validators reuse the exact same limits.
public sealed class Expense
{
    public const int DescriptionMinLength = 5;
    public const int DescriptionMaxLength = 500;
    public const int MaxAgeInDays = 90;

    public Guid Id { get; }
    public Guid TenantId { get; }
    public Guid EmployeeId { get; }
    public Money Amount { get; } = null!;
    public ExpenseCategory Category { get; }
    public string Description { get; } = null!;
    public DateOnly ExpenseDate { get; }

    // Status and the decision fields have private setters: they can only change
    // through Approve/Reject, never by assignment from outside the aggregate.
    public ExpenseStatus Status { get; private set; }
    public DateTimeOffset SubmittedAt { get; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public Guid? DecidedByEmployeeId { get; private set; }
    public RejectionReason? RejectionReason { get; private set; }

    // Parameterless constructor used only by EF Core to rehydrate from the
    // database; application code must go through the Submit factory.
    private Expense() { }

    private Expense(
        Guid id,
        Guid tenantId,
        Guid employeeId,
        Money amount,
        ExpenseCategory category,
        string description,
        DateOnly expenseDate,
        DateTimeOffset submittedAt)
    {
        Id = id;
        TenantId = tenantId;
        EmployeeId = employeeId;
        Amount = amount;
        Category = category;
        Description = description;
        ExpenseDate = expenseDate;
        Status = ExpenseStatus.Pending;
        SubmittedAt = submittedAt;
    }

    /// <summary>
    /// Creates a new expense in the Pending state. This is the only entry point
    /// for building an expense, so every field is validated up front and the
    /// expense inherits its owner's tenant (it is never passed in from outside).
    /// </summary>
    public static Expense Submit(
        Employee employee,
        Money amount,
        ExpenseCategory category,
        string description,
        DateOnly expenseDate,
        DateTimeOffset utcNow)
    {
        if (amount.Amount <= 0)
            throw new InvalidExpenseException("Amount must be greater than zero.");

        var trimmedDescription = description?.Trim() ?? string.Empty;
        if (trimmedDescription.Length is < DescriptionMinLength or > DescriptionMaxLength)
            throw new InvalidExpenseException(
                $"Description must have between {DescriptionMinLength} and {DescriptionMaxLength} characters.");

        // ExpenseDate must fall within [today - 90 days, today]. Comparing dates
        // (not timestamps) in UTC keeps the window free of time-zone ambiguity.
        var today = DateOnly.FromDateTime(utcNow.UtcDateTime);
        if (expenseDate > today)
            throw new InvalidExpenseException("Expense date cannot be in the future.");
        if (expenseDate < today.AddDays(-MaxAgeInDays))
            throw new InvalidExpenseException($"Expense date cannot be more than {MaxAgeInDays} days in the past.");

        // TenantId and EmployeeId come from the authenticated employee, never
        // from the request — this is what prevents submitting on behalf of others.
        return new Expense(
            Guid.NewGuid(), employee.TenantId, employee.Id, amount, category,
            trimmedDescription, expenseDate, utcNow);
    }

    /// <summary>
    /// Approves the expense. Enforces business rules 1–4: manager of the same
    /// tenant (1), not the submitter (2), still pending (3) and within the
    /// monthly limit (4).
    /// </summary>
    /// <param name="approvedTotalThisMonth">
    /// Sum of this employee's already-approved expenses in the expense's month,
    /// in the same currency as this expense. Provided by the caller because it
    /// requires querying other expenses, which a single aggregate cannot do.
    /// </param>
    public void Approve(Employee approver, Money approvedTotalThisMonth, decimal monthlyLimit, DateTimeOffset utcNow)
    {
        EnsureDecidableBy(approver);              // rule 1
        if (approver.Id == EmployeeId)            // rule 2: no self-approval
            throw new SelfApprovalException();
        EnsurePending();                          // rule 3: not already decided

        // Rule 4: the limit is checked against the running total *plus* this
        // expense. Money.Add throws if the currencies differ, so totals in other
        // currencies are never silently mixed in.
        var totalAfterApproval = approvedTotalThisMonth.Add(Amount);
        if (totalAfterApproval.Amount > monthlyLimit)
            throw new MonthlyLimitExceededException(totalAfterApproval, monthlyLimit);

        Status = ExpenseStatus.Approved;
        DecidedAt = utcNow;
        DecidedByEmployeeId = approver.Id;
    }

    /// <summary>
    /// Rejects the expense with a mandatory reason. Enforces rules 1, 3 and 5
    /// (the reason's 10–500 character length is guaranteed by RejectionReason).
    /// Self-rejection is allowed: it has no financial effect, unlike approval.
    /// </summary>
    public void Reject(Employee approver, RejectionReason reason, DateTimeOffset utcNow)
    {
        EnsureDecidableBy(approver);              // rule 1
        EnsurePending();                          // rule 3

        Status = ExpenseStatus.Rejected;
        RejectionReason = reason;
        DecidedAt = utcNow;
        DecidedByEmployeeId = approver.Id;
    }

    // Rule 1: a decision is only valid from a manager of the expense's own tenant.
    private void EnsureDecidableBy(Employee approver)
    {
        // Defense in depth: tenant isolation is primarily enforced at the query
        // layer (the approver can never load another tenant's expense), but the
        // aggregate refuses the cross-tenant decision regardless.
        if (approver.TenantId != TenantId)
            throw new CrossTenantDecisionException();
        if (!approver.IsManager)
            throw new ApproverNotManagerException();
    }

    // Rule 3: a Pending expense can be decided exactly once — there is no "undo".
    private void EnsurePending()
    {
        if (Status != ExpenseStatus.Pending)
            throw new ExpenseAlreadyDecidedException(Status);
    }
}

public sealed class InvalidExpenseException(string message) : DomainException(message);

public sealed class ApproverNotManagerException()
    : DomainException("Only managers can approve or reject expenses.");

public sealed class CrossTenantDecisionException()
    : DomainException("Expenses can only be decided by a manager of the same tenant.");

public sealed class SelfApprovalException()
    : DomainException("Managers cannot approve their own expenses.");

public sealed class ExpenseAlreadyDecidedException(ExpenseStatus status)
    : DomainException($"This expense was already decided (status: {status}). Decisions cannot be undone.");

public sealed class MonthlyLimitExceededException(Money totalAfterApproval, decimal monthlyLimit)
    : DomainException(
        $"Approving this expense would bring the employee's approved total for the month to " +
        $"{totalAfterApproval}, exceeding the tenant's monthly limit of {monthlyLimit:0.00}.");
