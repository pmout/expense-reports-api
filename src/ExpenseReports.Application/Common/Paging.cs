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
        Page = Math.Max(page, 1);
        PageSize = Math.Clamp(pageSize, 1, MaxPageSize);
    }

    public int Skip => (Page - 1) * PageSize;
}

public sealed record Page<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);

    public Page<TResult> Map<TResult>(Func<T, TResult> selector) =>
        new(Items.Select(selector).ToList(), PageNumber, PageSize, TotalCount);
}
