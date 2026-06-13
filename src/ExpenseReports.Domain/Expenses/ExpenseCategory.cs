namespace ExpenseReports.Domain.Expenses;

// The fixed set of expense types. Explicit values from 1 so a default 0 is never
// a valid category; persisted as text, so the numbers are only a safety anchor.
public enum ExpenseCategory
{
    Meal = 1,
    Transport = 2,
    Lodging = 3,
    Other = 4
}
