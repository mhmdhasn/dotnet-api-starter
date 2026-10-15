using Microsoft.EntityFrameworkCore;

namespace StarterKit.Api.Common.Pagination;

public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Clamps client input: page &gt;= 1, page size within 1..100 (default 20).</summary>
    public static (int Page, int PageSize) Normalize(int? page, int? pageSize)
    {
        int normalizedPage = page is null or < 1 ? 1 : page.Value;
        int normalizedSize = pageSize is null ? DefaultPageSize : Math.Clamp(pageSize.Value, 1, MaxPageSize);
        return (normalizedPage, normalizedSize);
    }

    /// <summary>
    /// Executes a count and a page query. The query MUST already be ordered, otherwise paging is not deterministic.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> orderedQuery,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken = default)
    {
        (int normalizedPage, int normalizedSize) = Normalize(page, pageSize);

        int totalCount = await orderedQuery.CountAsync(cancellationToken);
        int skip = (int)Math.Min((long)(normalizedPage - 1) * normalizedSize, int.MaxValue);

        List<T> items = await orderedQuery
            .Skip(skip)
            .Take(normalizedSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, normalizedPage, normalizedSize, totalCount);
    }
}
