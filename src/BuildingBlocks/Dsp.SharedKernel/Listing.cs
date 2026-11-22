using System.Globalization;
using System.Linq.Expressions;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Dsp.SharedKernel;

/// <summary>
/// What a list endpoint is asked for: search words (<c>q</c>), sort (<c>field</c> or <c>-field</c>), page, page size,
/// filter conditions (<c>filter</c>, JSON) and the reader's offset from UTC in minutes (<c>tz</c>, for calendar days).
/// Bind it with <c>[AsParameters]</c>. Every list endpoint filters, sorts and pages on the server with
/// <see cref="ListFields{T}"/>.
/// </summary>
public sealed record ListQuery(string? Q = null, string? Sort = null, int? Page = null, int? PageSize = null,
    string? Filter = null, int? Tz = null)
{
    public int PageNumber => Math.Max(1, Page ?? 1);
    public int Size => Math.Clamp(PageSize ?? PagedList<object>.DefaultPageSize, 1, PagedList<object>.MaxPageSize);
    public TimeSpan Offset => TimeSpan.FromMinutes(Math.Clamp(Tz ?? 0, -14 * 60, 14 * 60));
}

/// <summary>One filter condition, as the tables send it.</summary>
public sealed record ListCondition(string Column, string Op, string? Value, string? Value2, IReadOnlyList<string> Values);

/// <summary>The conditions of a list, combined with "all" or "any".</summary>
public sealed record ListFilter(bool MatchAny, IReadOnlyList<ListCondition> Conditions)
{
    public static readonly ListFilter None = new(false, []);

    /// <summary>Reads <c>{ "match": "all"|"any", "conditions": [{ column, op, value, value2, values }] }</c>; anything malformed is left out.</summary>
    public static ListFilter Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return None;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return None;
            var any = root.TryGetProperty("match", out var m) && m.ValueKind == JsonValueKind.String && m.GetString() == "any";
            var conditions = new List<ListCondition>();
            if (root.TryGetProperty("conditions", out var list) && list.ValueKind == JsonValueKind.Array)
                foreach (var c in list.EnumerateArray().Take(50))
                {
                    if (c.ValueKind != JsonValueKind.Object) continue;
                    var column = Str(c, "column");
                    var op = Str(c, "op");
                    if (string.IsNullOrEmpty(column) || string.IsNullOrEmpty(op)) continue;
                    var values = c.TryGetProperty("values", out var vs) && vs.ValueKind == JsonValueKind.Array
                        ? vs.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString()!).Take(200).ToList()
                        : [];
                    conditions.Add(new ListCondition(column, op, Str(c, "value"), Str(c, "value2"), values));
                }
            return new ListFilter(any, conditions);
        }
        catch (JsonException)
        {
            return None;
        }

        static string? Str(JsonElement e, string name) =>
            e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    }
}

/// <summary>A choice a list offers in its filters: the value and, when it isn't readable as is, its name.</summary>
public sealed record ListOption(string Value, LocalizedText? Label = null);

/// <summary>
/// The searchable, filterable and sortable fields of one list, as expressions over the rows, so the same rules run in
/// the database (EF Core translates them) or over rows in memory. Column ids match the tables' column ids.
/// Operators match the tables: text contains / notContains / equals / startsWith, number eq / gt / lt / between,
/// date on / before / after / range / lastDays (calendar days in the reader's time zone), choice anyOf / noneOf, and
/// empty / notEmpty for all.
/// </summary>
public sealed class ListFields<T>
{
    private readonly Dictionary<string, Field> fields = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Func<string, Expression<Func<T, bool>>?>> extraSearch = [];
    private readonly Func<IQueryable<T>, IOrderedQueryable<T>> defaultOrder;

    /// <param name="defaultOrder">The order when none is asked for; also breaks ties so pages are stable.</param>
    public ListFields(Func<IQueryable<T>, IOrderedQueryable<T>> defaultOrder) => this.defaultOrder = defaultOrder;

    public ListFields<T> Text(string id, Expression<Func<T, string?>> selector, bool searchable = true)
    {
        fields[id] = new TextField(selector, searchable);
        return this;
    }

    public ListFields<T> Number(string id, Expression<Func<T, decimal?>> selector)
    {
        fields[id] = new NumberField(selector);
        return this;
    }

    public ListFields<T> Date(string id, Expression<Func<T, DateTimeOffset?>> selector)
    {
        fields[id] = new DateField(selector);
        return this;
    }

    /// <summary>
    /// A column with a fixed set of values (status, process, service). <paramref name="parse"/> turns a value from the
    /// request into the stored value (e.g. an enum); <paramref name="options"/> lists the values with their names, for
    /// the filter and so search finds rows by name in any language. Without options, the values are looked up in the
    /// rows (<see cref="FacetQueries"/>).
    /// </summary>
    public ListFields<T> Choice<TValue>(string id, Expression<Func<T, TValue>> selector, Func<string, (bool Ok, TValue Value)> parse,
        IReadOnlyList<ListOption>? options = null, bool searchable = true)
    {
        fields[id] = new ChoiceField<TValue>(selector, parse, options, searchable);
        return this;
    }

    /// <summary>A text choice column (values stored as text).</summary>
    public ListFields<T> Choice(string id, Expression<Func<T, string?>> selector, IReadOnlyList<ListOption>? options = null,
        bool searchable = true) =>
        Choice<string?>(id, selector, v => (true, v), options, searchable);

    /// <summary>An enum choice column; values are the enum names.</summary>
    public ListFields<T> Enum<TEnum>(string id, Expression<Func<T, TEnum>> selector, IReadOnlyList<ListOption>? options = null)
        where TEnum : struct, System.Enum =>
        Choice(id, selector, v => (System.Enum.TryParse<TEnum>(v, true, out var e) && System.Enum.IsDefined(e), e),
            options ?? System.Enum.GetNames<TEnum>().Select(n => new ListOption(n)).ToList());

    /// <summary>A whole-number choice column (e.g. the process level).</summary>
    public ListFields<T> Int(string id, Expression<Func<T, int>> selector, IReadOnlyList<ListOption> options) =>
        Choice(id, selector, v => (int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n), n), options);

    /// <summary>A column holding several values per row (e.g. tags), filtered with anyOf / noneOf. In-memory lists only.</summary>
    public ListFields<T> Tags(string id, Expression<Func<T, IEnumerable<string>>> selector, IReadOnlyList<ListOption> options)
    {
        fields[id] = new TagsField(selector, options);
        return this;
    }

    /// <summary>More ways a search word can match a row (e.g. a service's name in any language, looked up elsewhere).</summary>
    public ListFields<T> SearchAlso(Func<string, Expression<Func<T, bool>>?> match)
    {
        extraSearch.Add(match);
        return this;
    }

    /// <summary>The rows that match the search words and conditions, in the asked order (not paged).</summary>
    public IQueryable<T> Apply(IQueryable<T> source, ListQuery query, DateTimeOffset? now = null)
    {
        var rows = source;
        foreach (var term in SearchTerms(query.Q))
        {
            var parts = fields.Values.Select(f => f.Search(term)).Concat(extraSearch.Select(s => s(term)))
                .Where(e => e is not null).Select(e => e!).ToList();
            rows = rows.Where(parts.Count == 0 ? False : Or(parts));
        }

        var filter = ListFilter.Parse(query.Filter);
        var conditions = filter.Conditions
            .Select(c => fields.TryGetValue(c.Column, out var f) ? f.Condition(c, query.Offset, now ?? DateTimeOffset.UtcNow) : null)
            .Where(e => e is not null).Select(e => e!).ToList();
        if (conditions.Count > 0) rows = rows.Where(filter.MatchAny ? Or(conditions) : And(conditions));

        return Order(rows, query.Sort);
    }

    /// <summary>Filters, sorts and cuts one page of rows held in memory.</summary>
    public PagedList<T> Page(IEnumerable<T> rows, ListQuery query, DateTimeOffset? now = null)
    {
        var matching = Apply(rows.AsQueryable(), query, now).ToList();
        var items = matching.Skip((query.PageNumber - 1) * query.Size).Take(query.Size).ToList();
        return new PagedList<T>(items, query.PageNumber, query.Size, matching.Count, Facets(rows));
    }

    /// <summary>The filter choices of every choice column: fixed options as given, and the rest found in <paramref name="rows"/> (in memory).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ListOption>> Facets(IEnumerable<T> rows)
    {
        var known = KnownFacets();
        var queryable = rows.AsQueryable();
        var found = FacetQueries(queryable).ToDictionary(f => f.Key,
            f => (IReadOnlyList<ListOption>)f.Value.ToList().Select(v => new ListOption(v)).ToList(), StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in known) found[k] = v;
        return found;
    }

    /// <summary>Filter choices that are fixed (given as options).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ListOption>> KnownFacets() =>
        fields.Where(f => f.Value.Options is not null).ToDictionary(f => f.Key, f => f.Value.Options!, StringComparer.OrdinalIgnoreCase);

    /// <summary>For text choice columns without fixed options: a query for their distinct values (run it with EF or in memory).</summary>
    public IReadOnlyDictionary<string, IQueryable<string>> FacetQueries(IQueryable<T> rows) =>
        fields.Where(f => f.Value.Options is null).Select(f => (f.Key, Query: f.Value.Distinct(rows)))
            .Where(f => f.Query is not null).ToDictionary(f => f.Key, f => f.Query!, StringComparer.OrdinalIgnoreCase);

    private IOrderedQueryable<T> Order(IQueryable<T> rows, string? sort)
    {
        if (!string.IsNullOrWhiteSpace(sort))
        {
            var desc = sort.StartsWith('-');
            if (fields.TryGetValue(desc ? sort[1..] : sort, out var field))
            {
                // Stable pages: the default order breaks ties.
                var ordered = field.Order(rows, desc);
                return ThenDefault(ordered);
            }
        }
        return defaultOrder(rows);
    }

    private IOrderedQueryable<T> ThenDefault(IOrderedQueryable<T> ordered)
    {
        // Apply the default order as a tie-breaker by re-expressing its OrderBy as ThenBy.
        var tail = defaultOrder(Enumerable.Empty<T>().AsQueryable()).Expression;
        return ThenByRewriter.Append(ordered, tail);
    }

    /// <summary>Search words: spaces separate words, "quoted text" stays together, compared in lower case.</summary>
    public static IReadOnlyList<string> SearchTerms(string? q) =>
        string.IsNullOrWhiteSpace(q)
            ? []
            : Regex.Matches(q, "\"([^\"]+)\"|(\\S+)", RegexOptions.None, TimeSpan.FromMilliseconds(200))
                .Select(m => (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).Trim().ToLowerInvariant())
                .Where(t => t.Length > 0).Take(10).ToList();

    // ---- expression helpers ----

    private static readonly Expression<Func<T, bool>> False = _ => false;

    private static Expression<Func<T, bool>> Or(IReadOnlyList<Expression<Func<T, bool>>> parts) => Combine(parts, Expression.OrElse);

    private static Expression<Func<T, bool>> And(IReadOnlyList<Expression<Func<T, bool>>> parts) => Combine(parts, Expression.AndAlso);

    private static Expression<Func<T, bool>> Combine(IReadOnlyList<Expression<Func<T, bool>>> parts,
        Func<Expression, Expression, BinaryExpression> join)
    {
        var p = Expression.Parameter(typeof(T), "x");
        var body = parts.Select(e => Replace(e.Body, e.Parameters[0], p)).Aggregate((a, b) => join(a, b));
        return Expression.Lambda<Func<T, bool>>(body, p);
    }

    /// <summary>pred(selector(row)) as one expression over the row.</summary>
    private static Expression<Func<T, bool>> On<TValue>(Expression<Func<T, TValue>> selector, Expression<Func<TValue, bool>> pred) =>
        Expression.Lambda<Func<T, bool>>(Replace(pred.Body, pred.Parameters[0], selector.Body), selector.Parameters);

    private static Expression Replace(Expression body, ParameterExpression from, Expression to) => new Swap(from, to).Visit(body);

    private sealed class Swap(ParameterExpression from, Expression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }

    private static decimal? Num(string? s) =>
        decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;

    private static DateTimeOffset? DayStart(string? s, TimeSpan offset) =>
        DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? new DateTimeOffset(d.ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime()
            : null;

    // ---- fields ----

    private abstract class Field
    {
        public virtual IReadOnlyList<ListOption>? Options => null;
        public abstract Expression<Func<T, bool>>? Condition(ListCondition c, TimeSpan offset, DateTimeOffset now);
        public abstract IOrderedQueryable<T> Order(IQueryable<T> rows, bool desc);
        public virtual Expression<Func<T, bool>>? Search(string term) => null;
        public virtual IQueryable<string>? Distinct(IQueryable<T> rows) => null;
    }

    private sealed class TextField(Expression<Func<T, string?>> selector, bool searchable) : Field
    {
        public override Expression<Func<T, bool>>? Condition(ListCondition c, TimeSpan offset, DateTimeOffset now)
        {
            if (c.Op == "empty") return On(selector, s => s == null || s == "");
            if (c.Op == "notEmpty") return On(selector, s => s != null && s != "");
            var v = (c.Value ?? "").Trim().ToLowerInvariant();
            if (v.Length == 0) return null;
            return c.Op switch
            {
                "contains" => On(selector, s => s != null && s.ToLower().Contains(v)),
                "notContains" => On(selector, s => s == null || !s.ToLower().Contains(v)),
                "equals" => On(selector, s => s != null && s.ToLower() == v),
                "startsWith" => On(selector, s => s != null && s.ToLower().StartsWith(v)),
                _ => null,
            };
        }

        public override Expression<Func<T, bool>>? Search(string term) =>
            searchable ? On(selector, s => s != null && s.ToLower().Contains(term)) : null;

        public override IOrderedQueryable<T> Order(IQueryable<T> rows, bool desc) =>
            desc ? rows.OrderByDescending(selector) : rows.OrderBy(selector);
    }

    private sealed class NumberField(Expression<Func<T, decimal?>> selector) : Field
    {
        public override Expression<Func<T, bool>>? Condition(ListCondition c, TimeSpan offset, DateTimeOffset now)
        {
            if (c.Op == "empty") return On(selector, n => n == null);
            if (c.Op == "notEmpty") return On(selector, n => n != null);
            var a = Num(c.Value);
            var b = Num(c.Value2);
            return c.Op switch
            {
                "eq" when a is not null => On(selector, n => n == a),
                "gt" when a is not null => On(selector, n => n > a),
                "lt" when a is not null => On(selector, n => n < a),
                "between" when a is not null && b is not null => On(selector, n => n >= a && n <= b),
                "between" when a is not null => On(selector, n => n >= a),
                "between" when b is not null => On(selector, n => n <= b),
                _ => null,
            };
        }

        public override IOrderedQueryable<T> Order(IQueryable<T> rows, bool desc) =>
            desc ? rows.OrderByDescending(selector) : rows.OrderBy(selector);
    }

    private sealed class DateField(Expression<Func<T, DateTimeOffset?>> selector) : Field
    {
        public override Expression<Func<T, bool>>? Condition(ListCondition c, TimeSpan offset, DateTimeOffset now)
        {
            if (c.Op == "empty") return On(selector, d => d == null);
            if (c.Op == "notEmpty") return On(selector, d => d != null);
            if (c.Op == "lastDays")
            {
                if (!int.TryParse(c.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) || days <= 0) return null;
                var today = DateOnly.FromDateTime(now.ToOffset(offset).DateTime);
                var from = new DateTimeOffset(today.AddDays(-days).ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime();
                var to = new DateTimeOffset(today.AddDays(1).ToDateTime(TimeOnly.MinValue), offset).ToUniversalTime();
                return On(selector, d => d >= from && d < to);
            }
            var a = DayStart(c.Value, offset);
            var b = DayStart(c.Value2, offset);
            switch (c.Op)
            {
                case "on" when a is { } start:
                    var end = start.AddDays(1);
                    return On(selector, d => d >= start && d < end);
                case "before" when a is { } before:
                    return On(selector, d => d < before);
                case "after" when a is { } after:
                    var next = after.AddDays(1);
                    return On(selector, d => d >= next);
                case "range":
                    var lo = a;
                    var hi = b?.AddDays(1);
                    if (lo is null && hi is null) return null;
                    if (lo is null) return On(selector, d => d < hi);
                    if (hi is null) return On(selector, d => d >= lo);
                    return On(selector, d => d >= lo && d < hi);
                default:
                    return null;
            }
        }

        public override IOrderedQueryable<T> Order(IQueryable<T> rows, bool desc) =>
            desc ? rows.OrderByDescending(selector) : rows.OrderBy(selector);
    }

    private sealed class ChoiceField<TValue>(Expression<Func<T, TValue>> selector, Func<string, (bool Ok, TValue Value)> parse,
        IReadOnlyList<ListOption>? options, bool searchable) : Field
    {
        public override IReadOnlyList<ListOption>? Options => options;

        public override Expression<Func<T, bool>>? Condition(ListCondition c, TimeSpan offset, DateTimeOffset now)
        {
            switch (c.Op)
            {
                case "empty":
                case "notEmpty":
                    var empty = Empty();
                    if (empty is null) return c.Op == "empty" ? False : null;
                    return c.Op == "empty" ? empty : Not(empty);
                case "anyOf":
                case "noneOf":
                    var values = Parse(c.Values);
                    if (values.Count == 0) return c.Op == "anyOf" && c.Values.Count > 0 ? False : null;
                    return c.Op == "anyOf" ? On(selector, v => values.Contains(v)) : On(selector, v => !values.Contains(v));
                default:
                    return null;
            }
        }

        public override Expression<Func<T, bool>>? Search(string term)
        {
            if (!searchable) return null;
            if (options is not null)
            {
                // By name in any language, or by value.
                var hits = options.Where(o => o.Value.Contains(term, StringComparison.OrdinalIgnoreCase)
                        || (o.Label?.Values.Any(l => l.Contains(term, StringComparison.OrdinalIgnoreCase)) ?? false))
                    .Select(o => o.Value).ToList();
                var values = Parse(hits);
                return values.Count == 0 ? null : On(selector, v => values.Contains(v));
            }
            if (typeof(TValue) == typeof(string))
            {
                var text = (Expression<Func<T, string?>>)(object)selector;
                return On(text, s => s != null && s.ToLower().Contains(term));
            }
            return null;
        }

        public override IQueryable<string>? Distinct(IQueryable<T> rows)
        {
            if (typeof(TValue) != typeof(string)) return null;
            var text = (Expression<Func<T, string?>>)(object)selector;
            return rows.Select(text).Where(v => v != null && v != "").Select(v => v!).Distinct().OrderBy(v => v).Take(200);
        }

        public override IOrderedQueryable<T> Order(IQueryable<T> rows, bool desc) =>
            desc ? rows.OrderByDescending(selector) : rows.OrderBy(selector);

        private List<TValue> Parse(IEnumerable<string> raw) =>
            raw.Select(parse).Where(p => p.Ok && p.Value is not null).Select(p => p.Value).Distinct().ToList();

        private Expression<Func<T, bool>>? Empty()
        {
            if (typeof(TValue) == typeof(string))
                return On((Expression<Func<T, string?>>)(object)selector, s => s == null || s == "");
            if (Nullable.GetUnderlyingType(typeof(TValue)) is not null || !typeof(TValue).IsValueType)
            {
                var body = Expression.Equal(selector.Body, Expression.Constant(null, typeof(TValue)));
                return Expression.Lambda<Func<T, bool>>(body, selector.Parameters);
            }
            return null;
        }

        private static Expression<Func<T, bool>> Not(Expression<Func<T, bool>> e) =>
            Expression.Lambda<Func<T, bool>>(Expression.Not(e.Body), e.Parameters);
    }

    private sealed class TagsField(Expression<Func<T, IEnumerable<string>>> selector, IReadOnlyList<ListOption> options) : Field
    {
        public override IReadOnlyList<ListOption>? Options => options;

        public override Expression<Func<T, bool>>? Condition(ListCondition c, TimeSpan offset, DateTimeOffset now)
        {
            var values = c.Values.ToList();
            return c.Op switch
            {
                "empty" => On(selector, xs => !xs.Any()),
                "notEmpty" => On(selector, xs => xs.Any()),
                "anyOf" when values.Count > 0 => On(selector, xs => xs.Any(x => values.Contains(x))),
                "noneOf" when values.Count > 0 => On(selector, xs => !xs.Any(x => values.Contains(x))),
                _ => null,
            };
        }

        public override Expression<Func<T, bool>>? Search(string term)
        {
            var hits = options.Where(o => o.Value.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (o.Label?.Values.Any(l => l.Contains(term, StringComparison.OrdinalIgnoreCase)) ?? false))
                .Select(o => o.Value).ToList();
            return hits.Count == 0 ? null : On(selector, xs => xs.Any(x => hits.Contains(x)));
        }

        public override IOrderedQueryable<T> Order(IQueryable<T> rows, bool desc) =>
            desc ? rows.OrderByDescending(Count()) : rows.OrderBy(Count());

        private Expression<Func<T, int>> Count()
        {
            Expression<Func<IEnumerable<string>, int>> count = xs => xs.Count();
            return Expression.Lambda<Func<T, int>>(Replace(count.Body, count.Parameters[0], selector.Body), selector.Parameters);
        }
    }

    /// <summary>Turns the default order's OrderBy/ThenBy calls into ThenBy calls on an already ordered query.</summary>
    private static class ThenByRewriter
    {
        public static IOrderedQueryable<T> Append(IOrderedQueryable<T> ordered, Expression defaultChain)
        {
            var calls = new Stack<MethodCallExpression>();
            for (var e = defaultChain; e is MethodCallExpression call; e = call.Arguments[0]) calls.Push(call);
            Expression current = ordered.Expression;
            foreach (var call in calls)
            {
                var name = call.Method.Name switch
                {
                    "OrderBy" or "ThenBy" => "ThenBy",
                    "OrderByDescending" or "ThenByDescending" => "ThenByDescending",
                    _ => null,
                };
                if (name is null) continue;
                var keyType = call.Method.GetGenericArguments()[1];
                var method = typeof(Queryable).GetMethods()
                    .Single(m => m.Name == name && m.GetParameters().Length == 2)
                    .MakeGenericMethod(typeof(T), keyType);
                current = Expression.Call(method, current, call.Arguments[1]);
            }
            return (IOrderedQueryable<T>)ordered.Provider.CreateQuery<T>(current);
        }
    }
}
