namespace ExpenseReports.Domain.Expenses;

// The expense lifecycle. Every expense starts Pending and moves exactly once to
// Approved or Rejected — both terminal. Values start at 1 so a default 0 is not a
// valid status. Persisted as text, so reordering would not corrupt stored data.
public enum ExpenseStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3
}
