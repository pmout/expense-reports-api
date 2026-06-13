namespace ExpenseReports.Domain.ValueObjects;

/// <summary>
/// Supported currencies. Enum values are the ISO 4217 numeric codes.
/// </summary>
// Explicit numeric values (rather than the default 0,1,2) anchor each member to
// its real-world ISO code, so reordering the list can never silently remap a
// currency. It is persisted by name, so the values are just a safety anchor.
public enum Currency
{
    USD = 840,
    EUR = 978,
    BRL = 986
}
