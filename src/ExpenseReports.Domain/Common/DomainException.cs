namespace ExpenseReports.Domain.Common;

/// <summary>
/// Base type for all business rule violations. The API layer maps these to
/// Problem Details responses instead of leaking stack traces.
/// </summary>
public abstract class DomainException(string message) : Exception(message);
