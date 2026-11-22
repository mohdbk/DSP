using Dsp.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Dsp.BuildingBlocks.Persistence;

/// <summary>Runs a list query in the database: search, conditions, sort and one page, plus the filter choices.</summary>
public static class ListQueries
{
    /// <param name="scope">The rows the caller may see (e.g. their own applications); facets come from these.</param>
    public static async Task<PagedList<T>> ToPageAsync<T>(this IQueryable<T> scope, ListFields<T> fields, ListQuery query,
        CancellationToken ct)
    {
        var matching = fields.Apply(scope, query);
        var total = await matching.CountAsync(ct);
        var items = await matching.Skip((query.PageNumber - 1) * query.Size).Take(query.Size).ToListAsync(ct);

        var facets = new Dictionary<string, IReadOnlyList<ListOption>>(fields.KnownFacets(), StringComparer.OrdinalIgnoreCase);
        foreach (var (id, values) in fields.FacetQueries(scope))
            facets[id] = (await values.ToListAsync(ct)).Select(v => new ListOption(v)).ToList();

        return new PagedList<T>(items, query.PageNumber, query.Size, total, facets);
    }
}
