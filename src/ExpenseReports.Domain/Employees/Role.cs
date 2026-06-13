namespace ExpenseReports.Domain.Employees;

// Explicit values starting at 1 (not the default 0) so an unset/default Role is
// never a valid one — a zeroed value can't accidentally read as "Employee".
public enum Role
{
    Employee = 1,
    Manager = 2
}
