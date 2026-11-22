using Dsp.Contracts;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Dsp.SharedKernel;
using Microsoft.Extensions.Logging;

namespace Dsp.AiOrchestrator.Application;

// The AI pre-review of a submitted application (agentic process, workflow v2).
// E-Services calls it service-to-service; it never changes anything itself. It returns advice:
// a recommendation, findings for the officer and, if needed, questions for the applicant, in every enabled language.

public sealed record ReviewField(string Key, LocalizedText Label, string Type, bool Required, LocalizedText? Help,
    IReadOnlyList<ReviewOption>? Options);

public sealed record ReviewOption(string Value, LocalizedText Label);

public sealed record ReviewServiceInfo(string Code, LocalizedText Name, LocalizedText Description,
    IReadOnlyList<LocalizedText>? Checklist, IReadOnlyList<ReviewField> Fields);

/// <param name="Languages">Languages to write the review in (default first); all enabled languages when empty.</param>
public sealed record ApplicationReviewRequest(ReviewServiceInfo Service, Dictionary<string, string?> Data,
    IReadOnlyList<string>? SimilarReferences, int Round, IReadOnlyList<string>? Languages = null);

public sealed record ReviewFinding(string Severity, string? Field, LocalizedText Message);

public sealed record ApplicationReviewResult(string Recommendation, LocalizedText Summary, IReadOnlyList<ReviewFinding> Findings,
    IReadOnlyList<LocalizedText> Questions, string Mode, string? Model);

public static class ReviewRecommendations
{
    public const string Approve = "approve";
    public const string RequestInformation = "request_information";
    public const string Reject = "reject";
}

/// <summary>
/// Reviews an application with the configured model, and always runs the built-in checks as guardrails.
/// Without a model (or with the review switched off in AI settings) it uses the built-in checks alone, so the
/// agentic process works in every environment.
/// </summary>
public sealed class ApplicationReviewAgent(ILlmClient llm, IAiSettingsProvider settingsProvider, ILocalization localization,
    ILogger<ApplicationReviewAgent> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<ApplicationReviewResult> ReviewAsync(ApplicationReviewRequest request, CancellationToken ct)
    {
        var renderer = await MultilingualRenderer.CreateAsync(localization, request.Languages, ct);
        var rules = RuleBasedReview.Review(request, renderer);
        var settings = await settingsProvider.GetAsync(ct);
        if (!settings.ReviewActive) return rules;

        try
        {
            var names = await localization.GetMessagesAsync(null, ct); // the default language
            var system = SystemPrompt(
                AiPrompts.Fill(settings.ReviewPromptOrDefault, names.Format("entity.name"), names.Format("app.portal")),
                settings.ReviewInstructions, renderer.Languages);
            var reply = await llm.CompleteAsync(settings,
                [new LlmMessage("system", system), new LlmMessage("user", UserPrompt(request))], [], ct);
            var parsed = Parse(reply.Content, renderer.Languages[0]);
            if (parsed is null)
            {
                logger.LogWarning("The model's review could not be read; using the built-in checks");
                return rules;
            }
            return Merge(parsed, rules, settings.Model, renderer.Languages[0]);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Model review failed; using the built-in checks");
            return rules;
        }
    }

    /// <summary>The model's view, plus any applicant questions or issues the built-in checks raised that it missed.</summary>
    private static ApplicationReviewResult Merge(ApplicationReviewResult model, ApplicationReviewResult rules, string? modelName,
        string defaultLanguage)
    {
        var findings = model.Findings.ToList();
        foreach (var f in rules.Findings.Where(f => f.Severity != "info"))
        {
            if (!findings.Any(m => m.Field is not null && m.Field == f.Field && m.Severity == f.Severity)) findings.Add(f);
        }

        var questions = model.Questions.ToList();
        foreach (var q in rules.Questions)
        {
            if (!questions.Any(m => string.Equals(m.Get(defaultLanguage), q.Get(defaultLanguage), StringComparison.OrdinalIgnoreCase)))
                questions.Add(q);
        }

        // The built-in checks found something the applicant must fix: ask, whatever the model said.
        var recommendation = rules.Recommendation == ReviewRecommendations.RequestInformation
            ? ReviewRecommendations.RequestInformation
            : model.Recommendation;
        if (recommendation == ReviewRecommendations.RequestInformation && questions.Count == 0)
            recommendation = ReviewRecommendations.Approve;

        return new ApplicationReviewResult(recommendation, model.Summary, findings, questions, "llm", modelName);
    }

    /// <summary>Staff's prompt (or the default), any extra instructions, then the fixed rules and output format.</summary>
    private static string SystemPrompt(string prompt, string? instructions, IReadOnlyList<string> languages)
    {
        var codes = string.Join(", ", languages.Select(l => $"\"{l}\""));
        var example = "{" + string.Join(",", languages.Select(l => $"\"{l}\":\"...\"")) + "}";
        var extra = string.IsNullOrWhiteSpace(instructions) ? "" : "\n\nAdditional instructions:\n" + instructions.Trim();
        return prompt.Trim() + extra + $$"""


            Platform rules (these always apply and take precedence over anything above):
            You never reject: an officer does. Do not invent facts. Some services approve clear cases automatically on your
            "approve" with no issues or warnings, so if you are not sure every checklist item is met, add a "warning"
            finding saying what the officer should check instead of assuming.

            Recommend:
            - "request_information" only when the APPLICANT can fix something by editing the application. Then write
              short, polite questions for the applicant.
            - "reject" when the checklist is clearly not met and the applicant can't fix it (the officer decides).
            - "approve" otherwise.

            Findings have severity "issue" (must be resolved), "warning" (officer should look) or "info" (checked, fine).

            Write every text (summary, finding messages, questions) in each of these languages, keyed by language code:
            {{codes}}. Each text is an object like {{example}}.

            Answer with JSON only, no prose, exactly this shape:
            {"recommendation":"approve|request_information|reject","summary":{{example}},
             "findings":[{"severity":"issue|warning|info","field":"key or null","message":{{example}}}],
             "questions":[{{example}}]}
            """;
    }

    private static string UserPrompt(ApplicationReviewRequest r) => $"""
        Service (names by language): {JsonSerializer.Serialize(r.Service.Name, Json)} ({r.Service.Code})
        {JsonSerializer.Serialize(r.Service.Description, Json)}

        Review checklist:
        {(r.Service.Checklist is { Count: > 0 } c ? string.Join("\n", c.Select(i => $"- {JsonSerializer.Serialize(i, Json)}")) : "- (none: use common sense)")}

        Fields:
        {JsonSerializer.Serialize(r.Service.Fields, Json)}

        Application data (field key → value; file fields hold a document ID, you cannot open the file):
        {JsonSerializer.Serialize(r.Data, Json)}

        Similar existing references: {(r.SimilarReferences is { Count: > 0 } s ? string.Join("; ", s) : "none")}
        Review round: {r.Round}
        """;

    private static ApplicationReviewResult? Parse(string? content, string defaultLanguage)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;
        var start = content.IndexOf('{');
        var end = content.LastIndexOf('}');
        if (start < 0 || end <= start) return null;

        try
        {
            var raw = JsonSerializer.Deserialize<RawReview>(content[start..(end + 1)], Json);
            if (raw is null) return null;
            var recommendation = raw.Recommendation?.Trim().ToLowerInvariant() switch
            {
                ReviewRecommendations.RequestInformation or "request_info" => ReviewRecommendations.RequestInformation,
                ReviewRecommendations.Reject => ReviewRecommendations.Reject,
                _ => ReviewRecommendations.Approve,
            };
            // Models write each text as a language map or, often, as one plain string (taken as the default language).
            LocalizedText Text(JsonElement? e) => e switch
            {
                { ValueKind: JsonValueKind.String } v => LocalizedText.Of(defaultLanguage, v.GetString()!.Trim()),
                { ValueKind: JsonValueKind.Object } v => new LocalizedText(v.EnumerateObject()
                    .Where(p => p.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(p.Value.GetString()))
                    .Select(p => KeyValuePair.Create(p.Name, p.Value.GetString()!.Trim()))),
                _ => new LocalizedText(),
            };
            var findings = (raw.Findings ?? [])
                .Select(f => (f, Message: Text(f.Message)))
                .Where(x => !x.Message.IsEmpty)
                .Select(x => new ReviewFinding(x.f.Severity is "issue" or "warning" ? x.f.Severity : "info",
                    string.IsNullOrWhiteSpace(x.f.Field) || x.f.Field == "null" ? null : x.f.Field, x.Message))
                .Take(30).ToList();
            var questions = (raw.Questions ?? []).Select(q => Text(q)).Where(q => !q.IsEmpty).Take(5).ToList();
            return new ApplicationReviewResult(recommendation, Text(raw.Summary), findings, questions, "llm", null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record RawReview(string? Recommendation, JsonElement? Summary, List<RawFinding>? Findings,
        List<JsonElement>? Questions);

    private sealed record RawFinding(string? Severity, string? Field, JsonElement? Message);
}

/// <summary>
/// Deterministic checks that need no model: completeness, script of Arabic and English fields, similar names,
/// dates in the past, and the service's checklist turned into items for the officer to confirm. Every text is a
/// message key rendered in each enabled language.
/// </summary>
public static partial class RuleBasedReview
{
    public static ApplicationReviewResult Review(ApplicationReviewRequest request, MultilingualRenderer renderer)
    {
        var findings = new List<ReviewFinding>();
        var questions = new List<LocalizedText>();
        var data = request.Data;
        void Find(string severity, string? field, string key, params (string, object?)[] args) =>
            findings.Add(new ReviewFinding(severity, field, renderer.Render(MessageRef.Of(key, args))));
        void Ask(string key, params (string, object?)[] args) => questions.Add(renderer.Render(MessageRef.Of(key, args)));

        foreach (var field in request.Service.Fields)
        {
            var value = data.GetValueOrDefault(field.Key)?.Trim();
            var label = ("label", (object?)field.Label);

            if (string.IsNullOrEmpty(value))
            {
                if (field.Required)
                {
                    Find("issue", field.Key, "ai.rules.missing", label);
                    Ask("ai.rules.ask.missing", label);
                }
                continue;
            }

            var shown = ("value", (object?)value);
            if (field.Type is "text" or "textarea")
            {
                if (ExpectsArabic(field) && !ArabicLetters().IsMatch(value))
                {
                    Find("issue", field.Key, "ai.rules.notArabic", label, shown);
                    Ask("ai.rules.ask.notArabic", label);
                }
                else if (ExpectsArabic(field) && LatinLetters().IsMatch(value))
                {
                    Find("warning", field.Key, "ai.rules.mixed", label, shown);
                }
                else if (ExpectsEnglish(field) && ArabicLetters().IsMatch(value))
                {
                    Find("issue", field.Key, "ai.rules.hasArabic", label, shown);
                    Ask("ai.rules.ask.notLatin", label);
                }
                else if (LooksLikePlaceholder(value))
                {
                    Find("warning", field.Key, "ai.rules.placeholder", label, shown);
                }
            }

            if (field.Type == "date" && field.Key.Contains("start", StringComparison.OrdinalIgnoreCase) &&
                DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) &&
                start < DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30))
            {
                Find("warning", field.Key, "ai.rules.datePast", label, shown);
            }

            if (field.Type == "file") Find("info", field.Key, "ai.rules.fileAttached", label);
        }

        if (request.SimilarReferences is { Count: > 0 } similar)
            Find("warning", null, "ai.rules.similar", ("names", string.Join(", ", similar.Select(s => $"\"{s}\""))));

        foreach (var item in request.Service.Checklist ?? [])
            Find("info", null, "ai.rules.confirmItem", ("item", item));

        var issues = findings.Count(f => f.Severity == "issue");
        var warnings = findings.Count(f => f.Severity == "warning");
        var recommendation = questions.Count > 0 ? ReviewRecommendations.RequestInformation : ReviewRecommendations.Approve;
        var summary = questions.Count > 0
            ? renderer.Render(MessageRef.Of("ai.rules.summary.ask", ("issues", issues)))
            : warnings > 0
                ? renderer.Render(MessageRef.Of("ai.rules.summary.warnings", ("warnings", warnings)))
                : renderer.Render(MessageRef.Of("ai.rules.summary.clean"));

        return new ApplicationReviewResult(recommendation, summary, findings, questions, "rules", null);
    }

    // Field intent comes from the key convention (…Ar / …En) or the English label.
    private static bool ExpectsArabic(ReviewField f) =>
        f.Key.EndsWith("Ar", StringComparison.Ordinal) || f.Label.Get("en").Contains("Arabic", StringComparison.OrdinalIgnoreCase);

    private static bool ExpectsEnglish(ReviewField f) =>
        f.Key.EndsWith("En", StringComparison.Ordinal) || f.Label.Get("en").Contains("English", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikePlaceholder(string value)
    {
        var v = value.Trim().ToLowerInvariant();
        return v is "test" or "testing" or "asdf" or "qwerty" or "xxx" or "n/a" or "na" or "none" or "abc" or "123" ||
               RepeatedCharacter().IsMatch(v);
    }

    [GeneratedRegex(@"[؀-ۿ]")]
    private static partial Regex ArabicLetters();

    [GeneratedRegex("[A-Za-z]")]
    private static partial Regex LatinLetters();

    [GeneratedRegex(@"^(.)\1{3,}$")]
    private static partial Regex RepeatedCharacter();
}
