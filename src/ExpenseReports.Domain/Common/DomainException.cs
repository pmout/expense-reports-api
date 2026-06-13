namespace ExpenseReports.Domain.Common;

/// <summary>
/// Base type for all business rule violations. The API layer maps these to
/// Problem Details responses instead of leaking stack traces.
/// </summary>
// `abstract` so it is never thrown directly — each rule throws its own specific
// subtype (SelfApprovalException, MonthlyLimitExceededException...), which makes
// failures self-describing and lets the API map each to the right HTTP status.
// The `(string message)` is a primary constructor that just forwards to the base
// Exception, sparing every subclass a constructor of its own.
public abstract class DomainException(string message) : Exception(message);
