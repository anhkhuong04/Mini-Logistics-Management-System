namespace MiniLogistics.Application.Common;

/// <summary>
/// Represents a repository-level page of data and the total number of matching records.
/// </summary>
/// <typeparam name="T">The item type contained in the page.</typeparam>
/// <param name="Items">The records contained in the requested page.</param>
/// <param name="PageNumber">The one-based page number returned by the repository.</param>
/// <param name="PageSize">The bounded page size used by the repository.</param>
/// <param name="TotalCount">The total number of records that match the query before paging.</param>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    /// <summary>
    /// Gets the number of pages implied by <see cref="TotalCount"/> and <see cref="PageSize"/>.
    /// </summary>
    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling((double)TotalCount / PageSize);
}
