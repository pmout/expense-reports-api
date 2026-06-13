using ExpenseReports.Application.Abstractions;
using ExpenseReports.Application.Common;
using ExpenseReports.Domain.Auditing;
using ExpenseReports.Domain.Expenses;

namespace ExpenseReports.Application.Expenses;

public sealed record RejectExpenseRequest(string Reason);

public sealed class RejectExpenseHandler(
    ICurrentUser currentUser,
    IExpenseRepository expenses,
    IEmployeeRepository employees,
    IExpenseAuditRepository auditLog,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<ExpenseResponse> HandleAsync(Guid expenseId, RejectExpenseRequest request, CancellationToken ct)
    {
        // No transaction or lock here (unlike approval): rejection touches no
        // running total, so the xmin concurrency token alone is enough to keep
        // the single state transition safe.
        var expense = await expenses.GetByIdAsync(expenseId, ct)
            ?? throw new NotFoundException("Expense");
        var approver = await employees.GetByIdAsync(currentUser.EmployeeId, ct)
            ?? throw new NotFoundException("Employee");

        // RejectionReason.Of enforces the 10–500 char rule before Reject runs.
        expense.Reject(approver, RejectionReason.Of(request.Reason), clock.GetUtcNow());

        // The audit row and the expense update are saved in a single SaveChanges,
        // i.e. one transaction — the record cannot be lost if the update succeeds.
        await auditLog.AddAsync(ExpenseAuditEntry.Record(expense), ct);

        await unitOfWork.SaveChangesAsync(ct);
        return ExpenseResponse.From(expense);
    }
}
