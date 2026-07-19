namespace MiniLogistics.Application.Common;

/// <summary>
/// Represents an application response page that can be returned to UI or API callers.
/// </summary>
/// <typeparam name="T">The response item type contained in the page.</typeparam>
/// <param name="Items">The records contained in the requested page.</param>
/// <param name="PageNumber">The one-based page number returned to the caller.</param>
/// <param name="PageSize">The bounded page size used by the application service.</param>
/// <param name="TotalCount">The total number of records that match the query before paging.</param>
public sealed record PagedResponse<T>(
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
