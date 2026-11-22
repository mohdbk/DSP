using System.Globalization;
using System.Text;
using Dsp.AiOrchestrator.Domain;
using Dsp.SharedKernel;

namespace Dsp.AiOrchestrator.Application;

/// <summary>
/// Deterministic chat used when no model is configured: walks the service's required fields in order, saving each
/// answer through the E-Services API. Same validation, same data, no AI needed. Every sentence is a message key, so
/// it speaks whichever language the portal uses, including languages added later.
/// </summary>
public sealed class GuidedConversation(IEServicesApi eservices, IMessages messages)
{
    private static readonly string[] DateFormats =
        ["yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "d MMM yyyy", "d MMMM yyyy", "MMM d yyyy", "MMMM d yyyy"];

    private string M(string key, params (string, object?)[] args) => messages.Format(key, args);

    // Words accepted in the reader's language ("chat.words.confirm" and "chat.words.yes" in the message catalogue).
    private IReadOnlyList<string> ConfirmWords => AgentPolicy.Words(M("chat.words.confirm"));
    private IReadOnlyList<string> YesWords => AgentPolicy.Words(M("chat.words.yes"));

    public async Task<ChatResponse> RespondAsync(ChatRequest request, ServiceView service, ApplicationView application,
        CancellationToken ct)
    {
        var lastUser = request.Messages.LastOrDefault(m => m.Role == ChatRoles.User)?.Content?.Trim();
        var prefix = new StringBuilder();

        // First turn: greet and confirm pre-filled details.
        if (string.IsNullOrEmpty(lastUser))
        {
            prefix.Append(M("chat.guided.greeting", ("service", messages.Pick(service.Name)))).Append(' ');
            var prefilled = service.AllFields.Where(f => HasValue(application, f.Key)).ToList();
            if (prefilled.Count > 0)
            {
                var list = string.Join(M("chat.listSeparator"),
                    prefilled.Select(f => $"{messages.Pick(f.Label)} ({application.Data[f.Key]})"));
                prefix.Append(M("chat.guided.prefilled", ("fields", list))).Append(' ');
            }
        }
        else if (AgentPolicy.Matches(lastUser, ConfirmWords) && NextMissing(service, application) is null)
        {
            var outcome = await eservices.SubmitAsync(application.Id, ct);
            if (outcome.Errors.Count == 0)
                return Reply(M("chat.guided.submitted", ("number", outcome.Application!.Number)), null, outcome.Application);

            var first = outcome.Errors[0];
            var field = service.AllFields.FirstOrDefault(f => f.Key == first.Field);
            application = await eservices.GetApplicationAsync(application.Id, ct);
            var cannot = M("chat.guided.cannotSubmit", ("message", first.Message));
            return field is null
                ? Reply(cannot, null, application)
                : Reply($"{cannot} {Ask(field)}", field.Key, application);
        }
        else if (request.PendingField is { } pendingKey &&
                 service.AllFields.FirstOrDefault(f => f.Key == pendingKey) is { } pending)
        {
            if (pending.Type == "file")
            {
                prefix.Append(M("chat.guided.filesNotInChat")).Append(' ');
            }
            else
            {
                var value = Interpret(pending, lastUser);
                if (value is null)
                    return Reply($"{M("chat.guided.notUnderstood")} {Ask(pending)}", pending.Key, application);

                var saved = await eservices.SaveDataAsync(application.Id,
                    new Dictionary<string, string?> { [pending.Key] = value }, ct);
                application = saved.Application;
                if (saved.Errors.Count > 0)
                    return Reply($"{saved.Errors[0].Message} {Ask(pending)}", pending.Key, application);
                prefix.Append(M("chat.guided.gotIt")).Append(' ');
            }
        }

        var next = NextMissing(service, application);
        if (next is null)
        {
            var summary = string.Join("\n", service.AllFields.Where(f => HasValue(application, f.Key) && f.Type != "checkbox")
                .Select(f => $"• {messages.Pick(f.Label)}: {Display(f, application.Data[f.Key])}"));
            return Reply(prefix + M("chat.guided.allFilled", ("summary", summary),
                    ("fee", service.Fee.ToString("#,0.##", CultureInfo.InvariantCulture)),
                    ("currency", M($"currency.{service.Currency}")),
                    ("word", ConfirmWords.FirstOrDefault() ?? "")),
                null, application);
        }

        if (next.Type == "file")
            return Reply(prefix + M("chat.guided.uploadNext", ("label", messages.Pick(next.Label))), next.Key, application);

        return Reply($"{prefix}{Ask(next)}", next.Key, application);
    }

    private static ChatResponse Reply(string text, string? pendingField, ApplicationView app) =>
        new(text.Trim(), pendingField, app, "guided");

    private static bool HasValue(ApplicationView app, string key) =>
        app.Data.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v);

    private static FieldView? NextMissing(ServiceView service, ApplicationView app) =>
        service.AllFields.FirstOrDefault(f => f.Required && !HasValue(app, f.Key));

    private string Ask(FieldView field)
    {
        var label = ("label", (object?)messages.Pick(field.Label));
        return field.Type switch
        {
            "select" => M("chat.ask.select", label,
                ("options", string.Join(M("chat.listSeparator"), field.Options!.Select(o => messages.Pick(o.Label))))),
            "date" => M("chat.ask.date", label),
            "checkbox" => M("chat.ask.checkbox", label, ("word", YesWords.FirstOrDefault() ?? "")),
            "file" => M("chat.ask.file", label),
            _ => M("chat.ask.text", label,
                ("help", messages.Pick(field.Help) is { Length: > 0 } help ? $" ({help})" : "")),
        };
    }

    private string Display(FieldView field, string? value) =>
        field.Options?.FirstOrDefault(o => o.Value == value) is { } option ? messages.Pick(option.Label) : value ?? "";

    private string? Interpret(FieldView field, string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        switch (field.Type)
        {
            case "checkbox":
                return AgentPolicy.Matches(input, YesWords) ? "true" : null;
            case "select":
                var options = field.Options ?? [];
                return options.FirstOrDefault(o => string.Equals(o.Value, input, StringComparison.OrdinalIgnoreCase) ||
                                                   o.Label.Values.Any(l => string.Equals(l, input, StringComparison.OrdinalIgnoreCase)))?.Value
                       ?? options.FirstOrDefault(o => o.Label.Values.Any(l => l.Contains(input, StringComparison.OrdinalIgnoreCase)))?.Value;
            case "date":
                return DateTime.TryParseExact(input, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                    ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : null;
            default:
                return input;
        }
    }
}
