namespace Dsp.SharedKernel;

/// <summary>
/// The one shape every list endpoint returns: <c>{ items, page, pageSize, total }</c>.
/// Pages are 1-based; <see cref="MaxPageSize"/> bounds a single response.
/// </summary>
public sealed record PagedList<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 500;
}

public static class PagedList
{
    /// <summary>Cuts one page out of an already ordered list.</summary>
    public static PagedList<T> From<T>(IReadOnlyList<T> all, int? page, int? pageSize)
    {
        var size = Math.Clamp(pageSize ?? PagedList<T>.DefaultPageSize, 1, PagedList<T>.MaxPageSize);
        var number = Math.Max(1, page ?? 1);
        return new PagedList<T>(all.Skip((number - 1) * size).Take(size).ToList(), number, size, all.Count);
    }
}
