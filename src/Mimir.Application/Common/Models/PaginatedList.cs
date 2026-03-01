namespace Mimir.Application.Common.Models;

/// <summary>
/// Represents a paginated subset of a collection, including metadata for page navigation.
/// </summary>
/// <typeparam name="T">The type of items in the paginated list.</typeparam>
public class PaginatedList<T>
{
    /// <summary>
    /// Gets the items on the current page.
    /// </summary>
    public IReadOnlyCollection<T> Items { get; private set; }

    /// <summary>
    /// Gets the current page number (1-based).
    /// </summary>
    public int PageNumber { get; private set; }

    /// <summary>
    /// Gets the total number of pages.
    /// </summary>
    public int TotalPages { get; private set; }

    /// <summary>
    /// Gets the total number of items across all pages.
    /// </summary>
    public int TotalCount { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PaginatedList{T}"/> class.
    /// </summary>
    /// <param name="items">The items on the current page.</param>
    /// <param name="pageNumber">The current page number.</param>
    /// <param name="totalPages">The total number of pages.</param>
    /// <param name="totalCount">The total number of items across all pages.</param>
    public PaginatedList(IReadOnlyCollection<T> items, int pageNumber, int totalPages, int totalCount)
    {
        Items = items;
        PageNumber = pageNumber;
        TotalPages = totalPages;
        TotalCount = totalCount;
    }

    /// <summary>
    /// Gets a value indicating whether a previous page exists.
    /// </summary>
    public bool HasPreviousPage => PageNumber > 1;

    /// <summary>
    /// Gets a value indicating whether a next page exists.
    /// </summary>
    public bool HasNextPage => PageNumber < TotalPages;

    /// <summary>
    /// Asynchronously creates a <see cref="PaginatedList{T}"/> from the specified queryable source.
    /// </summary>
    /// <param name="source">The queryable data source to paginate.</param>
    /// <param name="pageNumber">The page number to retrieve (1-based).</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <returns>A paginated list containing the requested page of items.</returns>
    public static async Task<PaginatedList<T>> CreateAsync(
        IQueryable<T> source,
        int pageNumber,
        int pageSize)
    {
        var totalCount = source.Count();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var items = await Task.FromResult(
            source
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList());

        return new PaginatedList<T>(items.AsReadOnly(), pageNumber, totalPages, totalCount);
    }
}
