namespace Dsp.SharedKernel;

/// <summary>
/// The one shape every list endpoint returns: <c>{ items, page, pageSize, total, facets }</c>.
/// Pages are 1-based; <see cref="MaxPageSize"/> bounds a single response. <c>facets</c> lists, per choice column,
/// the values the filters can offer (from all matching rows, not just this page).
/// </summary>
public sealed record PagedList<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total,
    IReadOnlyDictionary<string, IReadOnlyList<ListOption>>? Facets = null)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 500;

    /// <summary>The same page with each row turned into another shape (e.g. an entity into its DTO).</summary>
    public PagedList<TOut> Map<TOut>(Func<T, TOut> map) => new(Items.Select(map).ToList(), Page, PageSize, Total, Facets);

    public PagedList<T> WithFacets(IReadOnlyDictionary<string, IReadOnlyList<ListOption>> facets) => this with { Facets = facets };
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
