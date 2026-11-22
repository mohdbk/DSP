using System.Globalization;
using System.Text;
using Dsp.AiOrchestrator.Domain;

namespace Dsp.AiOrchestrator.Application;

/// <summary>
/// Deterministic chat used when no model is configured: walks the service's required fields in order,
/// saving each answer through the E-Services API. Same validation, same data, no AI needed.
/// </summary>
public sealed class GuidedConversation(IEServicesApi eservices)
{
    private static readonly string[] DateFormats =
        ["yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "d MMM yyyy", "d MMMM yyyy", "MMM d yyyy", "MMMM d yyyy"];

    public async Task<ChatResponse> RespondAsync(ChatRequest request, ServiceView service, ApplicationView application,
        CancellationToken ct)
    {
        var lastUser = request.Messages.LastOrDefault(m => m.Role == ChatRoles.User)?.Content?.Trim();
        var prefix = new StringBuilder();

        // First turn: greet and confirm pre-filled details.
        if (string.IsNullOrEmpty(lastUser))
        {
            prefix.Append($"Hello! I'll help you apply for {service.Name}. ");
            var prefilled = service.AllFields.Where(f => HasValue(application, f.Key)).ToList();
            if (prefilled.Count > 0)
            {
                prefix.Append("I've already filled in from your UAE PASS profile: ")
                    .Append(string.Join(", ", prefilled.Select(f => $"{f.Label} ({application.Data[f.Key]})")))
                    .Append(". You can change any of these in the form view. ");
            }
        }
        else if (AgentPolicy.ConfirmationWords.Contains(lastUser.ToLowerInvariant()) && NextMissing(service, application) is null)
        {
            var outcome = await eservices.SubmitAsync(application.Id, ct);
            if (outcome.Errors.Count == 0)
            {
                return Reply($"Done! Your application {outcome.Application!.Number} was submitted. " +
                             "A ministry officer will review it and you'll get a notification with the next step.",
                    null, outcome.Application);
            }

            var first = outcome.Errors[0];
            var field = service.AllFields.FirstOrDefault(f => f.Key == first.Field);
            application = await eservices.GetApplicationAsync(application.Id, ct);
            return field is null
                ? Reply($"I couldn't submit yet: {first.Message}", null, application)
                : Reply($"I couldn't submit yet: {first.Message} {Ask(field)}", field.Key, application);
        }
        else if (request.PendingField is { } pendingKey &&
                 service.AllFields.FirstOrDefault(f => f.Key == pendingKey) is { } pending)
        {
            if (pending.Type == "file")
            {
                prefix.Append("Files can't be sent through chat. ");
            }
            else
            {
                var value = Interpret(pending, lastUser);
                if (value is null) return Reply($"Sorry, I didn't catch that. {Ask(pending)}", pending.Key, application);

                var saved = await eservices.SaveDataAsync(application.Id,
                    new Dictionary<string, string?> { [pending.Key] = value }, ct);
                application = saved.Application;
                if (saved.Errors.Count > 0)
                    return Reply($"{saved.Errors[0].Message} {Ask(pending)}", pending.Key, application);
                prefix.Append("Got it. ");
            }
        }

        var next = NextMissing(service, application);
        if (next is null)
        {
            var summary = string.Join("\n", service.AllFields.Where(f => HasValue(application, f.Key) && f.Type != "checkbox")
                .Select(f => $"• {f.Label}: {Display(f, application.Data[f.Key])}"));
            return Reply($"{prefix}Everything is filled in:\n{summary}\n\nThe fee is {service.Fee:0.##} {service.Currency}, " +
                         "payable after approval. Type \"submit\" to send your application.", null, application);
        }

        if (next.Type == "file")
        {
            return Reply($"{prefix}Next I need the {next.Label}. Please switch to the form view to upload it, " +
                         "then come back here if you like.", next.Key, application);
        }

        return Reply($"{prefix}{Ask(next)}", next.Key, application);
    }

    private static ChatResponse Reply(string text, string? pendingField, ApplicationView app) =>
        new(text.Trim(), pendingField, app, "guided");

    private static bool HasValue(ApplicationView app, string key) =>
        app.Data.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v);

    private static FieldView? NextMissing(ServiceView service, ApplicationView app) =>
        service.AllFields.FirstOrDefault(f => f.Required && !HasValue(app, f.Key));

    private static string Ask(FieldView field) => field.Type switch
    {
        "select" => $"What is the {field.Label.ToLowerInvariant()}? Options: {string.Join(", ", field.Options!.Select(o => o.Label))}.",
        "date" => $"What is the {field.Label.ToLowerInvariant()}? (for example 2027-01-31)",
        "checkbox" => $"Please confirm: {field.Label}. Type \"yes\" to confirm.",
        "file" => $"Please upload the {field.Label} in the form view.",
        _ => $"What is the {field.Label.ToLowerInvariant()}?{(field.Help is null ? "" : $" ({field.Help})")}",
    };

    private static string Display(FieldView field, string? value) =>
        field.Options?.FirstOrDefault(o => o.Value == value)?.Label ?? value ?? "";

    private static string? Interpret(FieldView field, string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        switch (field.Type)
        {
            case "checkbox":
                return AgentPolicy.IsAffirmative(input) ? "true" : null;
            case "select":
                var options = field.Options ?? [];
                return options.FirstOrDefault(o => string.Equals(o.Value, input, StringComparison.OrdinalIgnoreCase) ||
                                                   string.Equals(o.Label, input, StringComparison.OrdinalIgnoreCase))?.Value
                       ?? options.FirstOrDefault(o => o.Label.Contains(input, StringComparison.OrdinalIgnoreCase))?.Value;
            case "date":
                return DateTime.TryParseExact(input, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                    ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : null;
            default:
                return input;
        }
    }
}
