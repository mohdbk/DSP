using Dsp.SharedKernel;

namespace Dsp.AiOrchestrator.Application;

/// <summary>
/// The A2UI component catalogue the portals render with AEGOV components. The agent may only use these, and every
/// property is data (text, bindings, options): nothing it sends can become markup or script.
/// </summary>
public static class A2UiCatalog
{
    public const string Id = "https://moet.gov.ae/dsp/a2ui/catalogs/aegov/v1";
    public const string Version = "1.0";
    public const string ActivityType = "a2ui";

    public const string SaveAction = "save_fields";
    public const string SubmitAction = "submit_application";
    public const string OpenFormAction = "open_form";

    // Components: Column, Text (text, variant: body|heading|hint), TextField (label, value, inputType, required, hint,
    // error), TextArea, DateField, ChoiceField (options [{value,label}]), CheckBox, FileUpload, Facts (items
    // [{label,value}]), Button (child, variant: primary|secondary, action).
}

/// <summary>
/// Builds the chat's screens from the service definition: the next missing answers as inputs (at most three, from one
/// step), or — when everything is filled — a summary with the fee and a submit button.
/// </summary>
public sealed class A2UiScreens(IMessages messages, ICurrencies currencies)
{
    public const int MaxFields = 3;

    public async Task<IReadOnlyList<Dictionary<string, object?>>?> NextAsync(ServiceView service, ApplicationView application,
        IReadOnlyList<FieldErrorView> errors, CancellationToken ct)
    {
        string? Value(string key) => application.Data.TryGetValue(key, out var v) ? v : null;
        bool Missing(FieldView f) => string.IsNullOrWhiteSpace(Value(f.Key)) || (f.Type == "checkbox" && f.Required && Value(f.Key) != "true");
        var withError = errors.Select(e => e.Field).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var step = service.Steps.FirstOrDefault(s => s.Fields.Any(f => withError.Contains(f.Key)))
                   ?? service.Steps.FirstOrDefault(s => s.Fields.Any(f => f.Required && Missing(f)));
        var surfaceId = $"s_{Guid.NewGuid():N}";
        return step is not null
            ? Inputs(surfaceId, step, step.Fields.Where(f => withError.Contains(f.Key) || (f.Required && Missing(f))).Take(MaxFields).ToList(),
                Value, errors)
            : await SummaryAsync(surfaceId, service, application, ct);
    }

    private List<Dictionary<string, object?>> Inputs(string surfaceId, StepView step, IReadOnlyList<FieldView> fields,
        Func<string, string?> value, IReadOnlyList<FieldErrorView> errors)
    {
        var components = new List<Dictionary<string, object?>>();
        var children = new List<string> { "title" };
        components.Add(C("title", "Text", ("text", messages.Pick(step.Title)), ("variant", "heading")));

        var values = new Dictionary<string, object?>();
        var context = new Dictionary<string, object?>();
        for (var i = 0; i < fields.Count; i++)
        {
            var f = fields[i];
            var id = $"field_{i}";
            children.Add(id);
            var bind = new Dictionary<string, object?> { ["@path"] = $"/values/{f.Key}" };
            var error = errors.FirstOrDefault(e => string.Equals(e.Field, f.Key, StringComparison.OrdinalIgnoreCase))?.Message;
            var common = new List<(string, object?)>
            {
                ("label", messages.Pick(f.Label)), ("value", bind), ("required", f.Required), ("name", f.Key),
                ("hint", f.Help is null ? null : messages.Pick(f.Help)), ("error", error),
            };
            var component = f.Type switch
            {
                "textarea" => "TextArea",
                "date" => "DateField",
                "select" => "ChoiceField",
                "checkbox" => "CheckBox",
                "file" => "FileUpload",
                _ => "TextField",
            };
            if (f.Type == "select")
                common.Add(("options", (f.Options ?? []).Select(o => new Dictionary<string, object?> { ["value"] = o.Value, ["label"] = messages.Pick(o.Label) }).ToList()));
            if (component == "TextField")
                common.Add(("inputType", f.Type is "email" or "number" ? f.Type : f.Type == "phone" ? "tel" : "text"));
            components.Add(C(id, component, common.ToArray()));
            values[f.Key] = f.Type == "checkbox" ? value(f.Key) == "true" : value(f.Key) ?? "";
            context[f.Key] = bind;
        }

        children.Add("save");
        components.Add(C("save", "Button", ("child", "save_label"), ("variant", "primary"), ("action", new Dictionary<string, object?>
        {
            ["event"] = new Dictionary<string, object?>
            {
                ["name"] = A2UiCatalog.SaveAction,
                ["userMessage"] = messages.Format("chat.a2ui.sent"),
                ["context"] = new Dictionary<string, object?> { ["values"] = context },
            },
        })));
        components.Add(C("save_label", "Text", ("text", messages.Format("form.saveContinue"))));
        components.Insert(0, C("root", "Column", ("children", children)));
        return Surface(surfaceId, components, new Dictionary<string, object?> { ["values"] = values });
    }

    private async Task<List<Dictionary<string, object?>>> SummaryAsync(string surfaceId, ServiceView service,
        ApplicationView application, CancellationToken ct)
    {
        var facts = service.AllFields
            .Where(f => f.Type != "checkbox" && application.Data.TryGetValue(f.Key, out var v) && !string.IsNullOrWhiteSpace(v))
            .Select(f => new Dictionary<string, object?> { ["label"] = messages.Pick(f.Label), ["value"] = Display(f, application.Data[f.Key]!) })
            .Append(new Dictionary<string, object?> { ["label"] = messages.Format("fee"), ["value"] = await currencies.FormatAsync(service.Fee, service.Currency, ct) })
            .ToList();

        var components = new List<Dictionary<string, object?>>
        {
            C("root", "Column", ("children", new[] { "title", "facts", "note", "submit", "change" })),
            C("title", "Text", ("text", messages.Format("chat.a2ui.checkTitle")), ("variant", "heading")),
            C("facts", "Facts", ("items", facts)),
            C("note", "Text", ("text", messages.Format("chat.a2ui.checkNote")), ("variant", "hint")),
            C("submit", "Button", ("child", "submit_label"), ("variant", "primary"), ("action", Event(A2UiCatalog.SubmitAction, messages.Format("chat.a2ui.submitSent")))),
            C("submit_label", "Text", ("text", messages.Format("form.submit"))),
            C("change", "Button", ("child", "change_label"), ("variant", "secondary"), ("action", Event(A2UiCatalog.OpenFormAction, null))),
            C("change_label", "Text", ("text", messages.Format("chat.a2ui.changeInForm"))),
        };
        return Surface(surfaceId, components, new Dictionary<string, object?>());
    }

    private string Display(FieldView field, string value) =>
        field.Options?.FirstOrDefault(o => o.Value == value) is { } option ? messages.Pick(option.Label) : value;

    private static Dictionary<string, object?> Event(string name, string? userMessage) => new()
    {
        ["event"] = new Dictionary<string, object?> { ["name"] = name, ["userMessage"] = userMessage },
    };

    private static Dictionary<string, object?> C(string id, string component, params (string Key, object? Value)[] props)
    {
        var c = new Dictionary<string, object?> { ["id"] = id, ["component"] = component };
        foreach (var (key, value) in props) if (value is not null) c[key] = value;
        return c;
    }

    private static List<Dictionary<string, object?>> Surface(string surfaceId, List<Dictionary<string, object?>> components,
        Dictionary<string, object?> dataModel) =>
    [
        new() { ["version"] = A2UiCatalog.Version, ["createSurface"] = new Dictionary<string, object?>
        {
            ["surfaceId"] = surfaceId, ["catalogId"] = A2UiCatalog.Id, ["sendDataModel"] = false,
        } },
        new() { ["version"] = A2UiCatalog.Version, ["updateComponents"] = new Dictionary<string, object?>
        {
            ["surfaceId"] = surfaceId, ["components"] = components,
        } },
        new() { ["version"] = A2UiCatalog.Version, ["updateDataModel"] = new Dictionary<string, object?>
        {
            ["surfaceId"] = surfaceId, ["path"] = "/", ["value"] = dataModel,
        } },
    ];
}
