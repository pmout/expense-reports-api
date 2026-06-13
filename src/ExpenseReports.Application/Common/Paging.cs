namespace ExpenseReports.Application.Common;

/// <summary>
/// Normalized pagination input: page is 1-based and the page size is capped,
/// so a client cannot request an unbounded result set.
/// </summary>
public sealed record PageRequest
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public int Page { get; }
    public int PageSize { get; }

    public PageRequest(int page = 1, int pageSize = DefaultPageSize)
    {
        // Sanitize in the constructor so the rest of the system can trust these
        // values: clamping the size to MaxPageSize stops a client requesting,
        // say, pageSize=1000000 and turning a list endpoint into a DoS.
        Page = Math.Max(page, 1);
        PageSize = Math.Clamp(pageSize, 1, MaxPageSize);
    }

    // Translates the 1-based page into the row offset the query needs.
    public int Skip => (Page - 1) * PageSize;
}

// A page of results plus the totals the client needs to render pagination.
// Map projects the items to another type while preserving the paging metadata —
// used to turn a Page<Expense> into a Page<ExpenseResponse> without re-querying.
public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public Page<TResult> Map<TResult>(Func<T, TResult> selector) =>
        new(Items.Select(selector).ToList(), PageNumber, PageSize, TotalCount);
}
