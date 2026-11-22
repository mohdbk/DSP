using System.Globalization;
using System.Text;
using Dsp.AiOrchestrator.Domain;

namespace Dsp.AiOrchestrator.Application;

/// <summary>
/// Deterministic chat used when no model is configured: walks the service's required fields in order,
/// saving each answer through the E-Services API. Same validation, same data, no AI needed.
/// Speaks English or Arabic, following the portal's language.
/// </summary>
public sealed class GuidedConversation(IEServicesApi eservices)
{
    private static readonly string[] DateFormats =
        ["yyyy-MM-dd", "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "d MMM yyyy", "d MMMM yyyy", "MMM d yyyy", "MMMM d yyyy"];

    public async Task<ChatResponse> RespondAsync(ChatRequest request, ServiceView service, ApplicationView application,
        CancellationToken ct)
    {
        var lang = request.Lang;
        string L(string en, string ar) => lang == "ar" ? ar : en;

        var lastUser = request.Messages.LastOrDefault(m => m.Role == ChatRoles.User)?.Content?.Trim();
        var prefix = new StringBuilder();

        // First turn: greet and confirm pre-filled details.
        if (string.IsNullOrEmpty(lastUser))
        {
            prefix.Append(L($"Hello! I'll help you apply for {service.Name}. ",
                $"مرحبًا! سأساعدك في تقديم طلب {service.NameFor(lang)}. "));
            var prefilled = service.AllFields.Where(f => HasValue(application, f.Key)).ToList();
            if (prefilled.Count > 0)
            {
                prefix.Append(L("I've already filled in from your UAE PASS profile: ", "عبأت مسبقًا من ملفك في الهوية الرقمية: "))
                    .Append(string.Join(L(", ", "، "), prefilled.Select(f => $"{f.LabelFor(lang)} ({application.Data[f.Key]})")))
                    .Append(L(". You can change any of these in the form view. ", ". يمكنك تعديل أي منها في النموذج. "));
            }
        }
        else if (AgentPolicy.IsConfirmation(lastUser) && NextMissing(service, application) is null)
        {
            var outcome = await eservices.SubmitAsync(application.Id, ct);
            if (outcome.Errors.Count == 0)
            {
                return Reply(L($"Done! Your application {outcome.Application!.Number} was submitted. " +
                               "A ministry officer will review it and you'll get a notification with the next step.",
                        $"تم! قُدّم طلبك رقم {outcome.Application!.Number}. سيراجعه موظف الوزارة وستصلك إشعارات بالخطوة التالية."),
                    null, outcome.Application);
            }

            var first = outcome.Errors[0];
            var field = service.AllFields.FirstOrDefault(f => f.Key == first.Field);
            application = await eservices.GetApplicationAsync(application.Id, ct);
            return field is null
                ? Reply(L($"I couldn't submit yet: {first.Message}", $"لم أتمكن من التقديم بعد: {first.Message}"), null, application)
                : Reply(L($"I couldn't submit yet: {first.Message} ", $"لم أتمكن من التقديم بعد: {first.Message} ") + Ask(field, lang),
                    field.Key, application);
        }
        else if (request.PendingField is { } pendingKey &&
                 service.AllFields.FirstOrDefault(f => f.Key == pendingKey) is { } pending)
        {
            if (pending.Type == "file")
            {
                prefix.Append(L("Files can't be sent through chat. ", "لا يمكن إرسال الملفات عبر المحادثة. "));
            }
            else
            {
                var value = Interpret(pending, lastUser);
                if (value is null)
                    return Reply(L("Sorry, I didn't catch that. ", "عذرًا، لم أفهم ذلك. ") + Ask(pending, lang), pending.Key, application);

                var saved = await eservices.SaveDataAsync(application.Id,
                    new Dictionary<string, string?> { [pending.Key] = value }, ct);
                application = saved.Application;
                if (saved.Errors.Count > 0)
                    return Reply($"{saved.Errors[0].Message} {Ask(pending, lang)}", pending.Key, application);
                prefix.Append(L("Got it. ", "تم. "));
            }
        }

        var next = NextMissing(service, application);
        if (next is null)
        {
            var summary = string.Join("\n", service.AllFields.Where(f => HasValue(application, f.Key) && f.Type != "checkbox")
                .Select(f => $"• {f.LabelFor(lang)}: {Display(f, application.Data[f.Key], lang)}"));
            return Reply(L($"{prefix}Everything is filled in:\n{summary}\n\nThe fee is {service.Fee:0.##} {service.Currency}, " +
                           "payable after approval. Type \"submit\" to send your application.",
                    $"{prefix}اكتملت جميع البيانات:\n{summary}\n\nالرسوم {service.Fee:0.##} {Currency(service.Currency, lang)}، " +
                    "تُدفع بعد الموافقة. اكتب \"إرسال\" لتقديم طلبك."), null, application);
        }

        if (next.Type == "file")
        {
            return Reply(L($"{prefix}Next I need the {next.Label}. Please switch to the form view to upload it, " +
                           "then come back here if you like.",
                    $"{prefix}أحتاج الآن إلى {next.LabelFor(lang)}. يرجى الانتقال إلى النموذج لرفعه، ثم العودة إلى هنا إن شئت."),
                next.Key, application);
        }

        return Reply($"{prefix}{Ask(next, lang)}", next.Key, application);
    }

    private static ChatResponse Reply(string text, string? pendingField, ApplicationView app) =>
        new(text.Trim(), pendingField, app, "guided");

    private static bool HasValue(ApplicationView app, string key) =>
        app.Data.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v);

    private static FieldView? NextMissing(ServiceView service, ApplicationView app) =>
        service.AllFields.FirstOrDefault(f => f.Required && !HasValue(app, f.Key));

    private static string Ask(FieldView field, string lang)
    {
        if (lang == "ar")
        {
            var label = field.LabelFor(lang);
            return field.Type switch
            {
                "select" => $"ما هو {label}؟ الخيارات: {string.Join("، ", field.Options!.Select(o => o.LabelFor(lang)))}.",
                "date" => $"ما هو {label}؟ (مثلًا 2027-01-31)",
                "checkbox" => $"يرجى التأكيد: {label}. اكتب \"نعم\" للتأكيد.",
                "file" => $"يرجى رفع {label} في النموذج.",
                _ => $"ما هو {label}؟{(field.HelpFor(lang) is { } help ? $" ({help})" : "")}",
            };
        }

        return field.Type switch
        {
            "select" => $"What is the {field.Label.ToLowerInvariant()}? Options: {string.Join(", ", field.Options!.Select(o => o.Label))}.",
            "date" => $"What is the {field.Label.ToLowerInvariant()}? (for example 2027-01-31)",
            "checkbox" => $"Please confirm: {field.Label}. Type \"yes\" to confirm.",
            "file" => $"Please upload the {field.Label} in the form view.",
            _ => $"What is the {field.Label.ToLowerInvariant()}?{(field.Help is null ? "" : $" ({field.Help})")}",
        };
    }

    private static string Display(FieldView field, string? value, string lang) =>
        field.Options?.FirstOrDefault(o => o.Value == value)?.LabelFor(lang) ?? value ?? "";

    private static string Currency(string currency, string lang) => lang == "ar" && currency == "AED" ? "درهم" : currency;

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
                                                   string.Equals(o.Label, input, StringComparison.OrdinalIgnoreCase) ||
                                                   string.Equals(o.LabelAr, input, StringComparison.Ordinal))?.Value
                       ?? options.FirstOrDefault(o => o.Label.Contains(input, StringComparison.OrdinalIgnoreCase) ||
                                                      (o.LabelAr?.Contains(input, StringComparison.Ordinal) ?? false))?.Value;
            case "date":
                return DateTime.TryParseExact(input, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                    ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                    : null;
            default:
                return input;
        }
    }
}
