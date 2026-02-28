namespace Mimir.Application.Common.Models;

public class PaginatedList<T>
{
    public IReadOnlyCollection<T> Items { get; private set; }

    public int PageNumber { get; private set; }

    public int TotalPages { get; private set; }

    public int TotalCount { get; private set; }

    public PaginatedList(IReadOnlyCollection<T> items, int pageNumber, int totalPages, int totalCount)
    {
        Items = items;
        PageNumber = pageNumber;
        TotalPages = totalPages;
        TotalCount = totalCount;
    }

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;

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
