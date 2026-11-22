using Dsp.SharedKernel;

namespace Dsp.UnitTests.SharedKernel;

/// <summary>The list engine every list endpoint uses: search words, typed conditions, sorting and paging.</summary>
public class ListFieldsTests
{
    private enum State { Open, Closed }

    private sealed record Row(string Number, string? Name, decimal Fee, DateTimeOffset At, State State, string Service, int Level,
        IReadOnlyList<string> Tags);

    private static readonly DateTimeOffset Now = new(2026, 11, 22, 10, 0, 0, TimeSpan.Zero);

    private static readonly Row[] Rows =
    [
        new("A-1", "Falcon Trading", 100, new(2026, 11, 20, 21, 30, 0, TimeSpan.Zero), State.Open, "TRADE", 1, ["portal"]),
        new("A-2", "Desert Rose", 250.5m, new(2026, 11, 21, 8, 0, 0, TimeSpan.Zero), State.Closed, "AGENCY", 2, ["portal", "backoffice"]),
        new("A-3", null, 0, new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero), State.Open, "TRADE", 3, []),
    ];

    private static ListFields<Row> Fields() =>
        new ListFields<Row>(q => q.OrderBy(r => r.Number))
            .Text("number", r => r.Number)
            .Text("name", r => r.Name)
            .Number("fee", r => r.Fee)
            .Date("at", r => r.At)
            .Enum("state", r => r.State, [new ListOption("Open", LocalizedText.Of("en", "In progress")), new ListOption("Closed", LocalizedText.Of("en", "Done"))])
            .Choice("service", r => r.Service, [new ListOption("TRADE", LocalizedText.Of("en", "Trade name")), new ListOption("AGENCY", LocalizedText.Of("en", "Commercial agency"))])
            .Int("level", r => r.Level, [new ListOption("1"), new ListOption("2"), new ListOption("3")])
            .Tags("tags", r => r.Tags, [new ListOption("portal"), new ListOption("backoffice")]);

    private static string[] Numbers(ListQuery query) => Fields().Page(Rows, query, Now).Items.Select(r => r.Number).ToArray();

    private static string Filter(string column, string op, string? value = null, string? value2 = null, string[]? values = null) =>
        System.Text.Json.JsonSerializer.Serialize(new { match = "all", conditions = new[] { new { column, op, value, value2, values = values ?? [] } } });

    [Fact]
    public void Every_search_word_must_match_some_column_case_insensitively()
    {
        Assert.Equal(new[] { "A-1" }, Numbers(new ListQuery(Q: "falcon TRAD")));
        Assert.Empty(Numbers(new ListQuery(Q: "falcon rose")));
        Assert.Equal(new[] { "A-2" }, Numbers(new ListQuery(Q: "\"desert rose\"")));
    }

    [Fact]
    public void Search_finds_choices_by_their_name()
    {
        Assert.Equal(new[] { "A-2" }, Numbers(new ListQuery(Q: "agency")));
        Assert.Equal(new[] { "A-2" }, Numbers(new ListQuery(Q: "done")));
    }

    [Fact]
    public void Text_conditions()
    {
        Assert.Equal(new[] { "A-1" }, Numbers(new ListQuery(Filter: Filter("name", "startsWith", "fal"))));
        Assert.Equal(new[] { "A-2", "A-3" }, Numbers(new ListQuery(Filter: Filter("name", "notContains", "falcon"))));
        Assert.Equal(new[] { "A-3" }, Numbers(new ListQuery(Filter: Filter("name", "empty"))));
    }

    [Fact]
    public void Number_conditions()
    {
        Assert.Equal(new[] { "A-2" }, Numbers(new ListQuery(Filter: Filter("fee", "gt", "100"))));
        Assert.Equal(new[] { "A-1", "A-2" }, Numbers(new ListQuery(Filter: Filter("fee", "between", "100", "300"))));
    }

    [Fact]
    public void Dates_are_calendar_days_in_the_readers_time_zone()
    {
        // 20 Nov 21:30 UTC is already 21 Nov in Dubai (UTC+4).
        Assert.Equal(new[] { "A-1", "A-2" }, Numbers(new ListQuery(Tz: 240, Filter: Filter("at", "on", "2026-11-21"))));
        Assert.Equal(new[] { "A-1" }, Numbers(new ListQuery(Tz: 0, Filter: Filter("at", "on", "2026-11-20"))));
        Assert.Equal(new[] { "A-1", "A-2" }, Numbers(new ListQuery(Tz: 240, Filter: Filter("at", "lastDays", "7"))));
        Assert.Equal(new[] { "A-3" }, Numbers(new ListQuery(Filter: Filter("at", "before", "2026-11-01"))));
    }

    [Fact]
    public void Choice_and_tag_conditions()
    {
        Assert.Equal(new[] { "A-1", "A-3" }, Numbers(new ListQuery(Filter: Filter("state", "anyOf", values: ["Open"]))));
        Assert.Equal(new[] { "A-2", "A-3" }, Numbers(new ListQuery(Filter: Filter("level", "anyOf", values: ["2", "3"]))));
        Assert.Equal(new[] { "A-3" }, Numbers(new ListQuery(Filter: Filter("tags", "empty"))));
        Assert.Equal(new[] { "A-3" }, Numbers(new ListQuery(Filter: Filter("tags", "noneOf", values: ["portal"]))));
        // Values the column doesn't have match nothing rather than everything.
        Assert.Empty(Numbers(new ListQuery(Filter: Filter("state", "anyOf", values: ["Unknown"]))));
    }

    [Fact]
    public void Conditions_combine_with_all_or_any_and_unknown_columns_are_ignored()
    {
        const string any = """{"match":"any","conditions":[{"column":"fee","op":"gt","value":"200"},{"column":"name","op":"empty"},{"column":"nope","op":"eq","value":"1"}]}""";
        Assert.Equal(new[] { "A-2", "A-3" }, Numbers(new ListQuery(Filter: any)));
        Assert.Equal(new[] { "A-1", "A-2", "A-3" }, Numbers(new ListQuery(Filter: "not json")));
    }

    [Fact]
    public void Sorting_and_paging()
    {
        Assert.Equal(new[] { "A-2", "A-1", "A-3" }, Numbers(new ListQuery(Sort: "-fee")));
        var page = Fields().Page(Rows, new ListQuery(Sort: "fee", Page: 2, PageSize: 2), Now);
        Assert.Equal(3, page.Total);
        Assert.Equal(new[] { "A-2" }, page.Items.Select(r => r.Number));
        // Unknown sort falls back to the default order.
        Assert.Equal(new[] { "A-1", "A-2", "A-3" }, Numbers(new ListQuery(Sort: "nope")));
    }

    [Fact]
    public void Facets_offer_fixed_options_and_the_values_found()
    {
        var facets = Fields().Page(Rows, new ListQuery(), Now).Facets!;
        Assert.Equal(new[] { "TRADE", "AGENCY" }, facets["service"].Select(o => o.Value));
        Assert.Equal(new[] { "Open", "Closed" }, facets["state"].Select(o => o.Value));
    }
}
