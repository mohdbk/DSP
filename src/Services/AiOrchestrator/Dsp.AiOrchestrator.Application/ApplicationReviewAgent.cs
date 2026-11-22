using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Dsp.AiOrchestrator.Application;

// The AI pre-review of a submitted application (agentic process, workflow v2).
// E-Services calls it service-to-service; it never changes anything itself. It returns advice:
// a recommendation, findings for the officer and, if needed, questions for the applicant.

public sealed record ReviewField(string Key, string Label, string? LabelAr, string Type, bool Required, string? Help,
    IReadOnlyList<ReviewOption>? Options);

public sealed record ReviewOption(string Value, string Label);

public sealed record ReviewServiceInfo(string Code, string Name, string? NameAr, string Description,
    IReadOnlyList<string>? Checklist, IReadOnlyList<ReviewField> Fields);

public sealed record ApplicationReviewRequest(ReviewServiceInfo Service, Dictionary<string, string?> Data,
    IReadOnlyList<string>? SimilarReferences, int Round);

public sealed record ReviewFinding(string Severity, string? Field, string Message);

public sealed record ReviewQuestion(string En, string Ar);

public sealed record ApplicationReviewResult(string Recommendation, string Summary, IReadOnlyList<ReviewFinding> Findings,
    IReadOnlyList<ReviewQuestion> Questions, string Mode, string? Model);

public static class ReviewRecommendations
{
    public const string Approve = "approve";
    public const string RequestInformation = "request_information";
    public const string Reject = "reject";
}

/// <summary>
/// Reviews an application with the configured model, and always runs the built-in checks as guardrails.
/// Without a model it uses the built-in checks alone, so the agentic process works in every environment.
/// </summary>
public sealed class ApplicationReviewAgent(ILlmClient llm, ILogger<ApplicationReviewAgent> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<ApplicationReviewResult> ReviewAsync(ApplicationReviewRequest request, CancellationToken ct)
    {
        var rules = RuleBasedReview.Review(request);
        if (!llm.IsConfigured) return rules;

        try
        {
            var reply = await llm.CompleteAsync(
                [new LlmMessage("system", SystemPrompt), new LlmMessage("user", UserPrompt(request))], [], ct);
            var parsed = Parse(reply.Content);
            if (parsed is null)
            {
                logger.LogWarning("The model's review could not be read; using the built-in checks");
                return rules;
            }
            return Merge(parsed, rules, llm.Model);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Model review failed; using the built-in checks");
            return rules;
        }
    }

    /// <summary>The model's view, plus any applicant questions or issues the built-in checks raised that it missed.</summary>
    private static ApplicationReviewResult Merge(ApplicationReviewResult model, ApplicationReviewResult rules, string? modelName)
    {
        var findings = model.Findings.ToList();
        foreach (var f in rules.Findings.Where(f => f.Severity != "info"))
        {
            if (!findings.Any(m => m.Field is not null && m.Field == f.Field && m.Severity == f.Severity)) findings.Add(f);
        }

        var questions = model.Questions.ToList();
        foreach (var q in rules.Questions)
        {
            if (!questions.Any(m => string.Equals(m.En, q.En, StringComparison.OrdinalIgnoreCase))) questions.Add(q);
        }

        // The built-in checks found something the applicant must fix: ask, whatever the model said.
        var recommendation = rules.Recommendation == ReviewRecommendations.RequestInformation
            ? ReviewRecommendations.RequestInformation
            : model.Recommendation;
        if (recommendation == ReviewRecommendations.RequestInformation && questions.Count == 0)
            recommendation = ReviewRecommendations.Approve;

        return new ApplicationReviewResult(recommendation, model.Summary, findings, questions, "llm", modelName);
    }

    private const string SystemPrompt = """
        You are the pre-review assistant of the UAE Ministry of Economy and Tourism. Before a ministry officer
        decides on an e-service application, you check it and prepare the case. You never decide: the officer does.

        Check the application against the service's review checklist and common sense:
        - Is anything missing, inconsistent or implausible? (e.g. an Arabic field not written in Arabic,
          names that don't match, dates that don't make sense, a trade name that imitates a brand or a government body).
        - Is the reference (e.g. trade name) confusingly similar to the similar existing references listed?

        Recommend:
        - "request_information" only when the APPLICANT can fix something by editing the application. Then write
          short, polite questions for the applicant, each in English ("en") and Arabic ("ar").
        - "reject" when the checklist is clearly not met and the applicant can't fix it (the officer decides).
        - "approve" otherwise.

        Write findings for the officer: severity "issue" (must be resolved), "warning" (officer should look) or
        "info" (checked, fine). Refer to fields by their key. Be specific and brief. Do not invent facts.

        Answer with JSON only, no prose, exactly this shape:
        {"recommendation":"approve|request_information|reject","summary":"one or two sentences for the officer",
         "findings":[{"severity":"issue|warning|info","field":"key or null","message":"..."}],
         "questions":[{"en":"...","ar":"..."}]}
        """;

    private static string UserPrompt(ApplicationReviewRequest r) => $"""
        Service: {r.Service.Name} ({r.Service.Code})
        {r.Service.Description}

        Review checklist:
        {(r.Service.Checklist is { Count: > 0 } c ? string.Join("\n", c.Select(i => $"- {i}")) : "- (none: use common sense)")}

        Fields:
        {JsonSerializer.Serialize(r.Service.Fields, Json)}

        Application data (field key → value; file fields hold a document ID, you cannot open the file):
        {JsonSerializer.Serialize(r.Data, Json)}

        Similar existing references: {(r.SimilarReferences is { Count: > 0 } s ? string.Join("; ", s) : "none")}
        Review round: {r.Round}
        """;

    private static ApplicationReviewResult? Parse(string? content)
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
            var findings = (raw.Findings ?? [])
                .Where(f => !string.IsNullOrWhiteSpace(f.Message))
                .Select(f => new ReviewFinding(f.Severity is "issue" or "warning" ? f.Severity : "info",
                    string.IsNullOrWhiteSpace(f.Field) || f.Field == "null" ? null : f.Field, f.Message!.Trim()))
                .Take(30).ToList();
            var questions = (raw.Questions ?? [])
                .Where(q => !string.IsNullOrWhiteSpace(q.En))
                .Select(q => new ReviewQuestion(q.En!.Trim(), string.IsNullOrWhiteSpace(q.Ar) ? q.En!.Trim() : q.Ar!.Trim()))
                .Take(5).ToList();
            return new ApplicationReviewResult(recommendation, raw.Summary?.Trim() ?? "", findings, questions, "llm", null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record RawReview(string? Recommendation, string? Summary, List<RawFinding>? Findings,
        List<RawQuestion>? Questions);

    private sealed record RawFinding(string? Severity, string? Field, string? Message);

    private sealed record RawQuestion(string? En, string? Ar);
}

/// <summary>
/// Deterministic checks that need no model: completeness, script of Arabic and English fields, similar names,
/// dates in the past, and the service's checklist turned into items for the officer to confirm.
/// </summary>
public static partial class RuleBasedReview
{
    public static ApplicationReviewResult Review(ApplicationReviewRequest request)
    {
        var findings = new List<ReviewFinding>();
        var questions = new List<ReviewQuestion>();
        var data = request.Data;

        foreach (var field in request.Service.Fields)
        {
            var value = data.GetValueOrDefault(field.Key)?.Trim();
            var labelAr = field.LabelAr ?? field.Label;

            if (string.IsNullOrEmpty(value))
            {
                if (field.Required)
                {
                    findings.Add(new ReviewFinding("issue", field.Key, $"{field.Label} is missing."));
                    questions.Add(new ReviewQuestion($"Please provide {field.Label}.", $"يرجى تقديم {labelAr}."));
                }
                continue;
            }

            if (field.Type is "text" or "textarea")
            {
                if (ExpectsArabic(field) && !ArabicLetters().IsMatch(value))
                {
                    findings.Add(new ReviewFinding("issue", field.Key, $"{field.Label} is not written in Arabic letters (\"{value}\")."));
                    questions.Add(new ReviewQuestion(
                        $"{field.Label} must be written in Arabic letters. Please correct it.",
                        $"يجب كتابة {labelAr} بالحروف العربية. يرجى تصحيحه."));
                }
                else if (ExpectsArabic(field) && LatinLetters().IsMatch(value))
                {
                    findings.Add(new ReviewFinding("warning", field.Key, $"{field.Label} mixes Arabic and Latin letters (\"{value}\")."));
                }
                else if (ExpectsEnglish(field) && ArabicLetters().IsMatch(value))
                {
                    findings.Add(new ReviewFinding("issue", field.Key, $"{field.Label} contains Arabic letters (\"{value}\")."));
                    questions.Add(new ReviewQuestion(
                        $"{field.Label} must be written in English (Latin) letters. Please correct it.",
                        $"يجب كتابة {labelAr} بالحروف الإنجليزية. يرجى تصحيحه."));
                }
                else if (LooksLikePlaceholder(value))
                {
                    findings.Add(new ReviewFinding("warning", field.Key, $"{field.Label} looks like a placeholder or test value (\"{value}\")."));
                }
            }

            if (field.Type == "date" && field.Key.Contains("start", StringComparison.OrdinalIgnoreCase) &&
                DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start) &&
                start < DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-30))
            {
                findings.Add(new ReviewFinding("warning", field.Key, $"{field.Label} ({value}) is more than 30 days in the past."));
            }

            if (field.Type == "file")
                findings.Add(new ReviewFinding("info", field.Key, $"{field.Label} is attached. Open it to check it matches the application."));
        }

        if (request.SimilarReferences is { Count: > 0 } similar)
        {
            findings.Add(new ReviewFinding("warning", null,
                $"Similar to existing applications: {string.Join(", ", similar.Select(s => $"\"{s}\""))}. Check they can't be confused."));
        }

        foreach (var item in request.Service.Checklist ?? [])
            findings.Add(new ReviewFinding("info", null, $"Officer to confirm: {item}"));

        var issues = findings.Count(f => f.Severity == "issue");
        var warnings = findings.Count(f => f.Severity == "warning");
        var recommendation = questions.Count > 0 ? ReviewRecommendations.RequestInformation : ReviewRecommendations.Approve;
        var summary = questions.Count > 0
            ? $"The built-in checks found {issues} issue(s) the applicant can fix; the applicant is asked to correct them before staff review."
            : warnings > 0
                ? $"No blocking issues. {warnings} point(s) for the officer to look at."
                : "No issues found by the built-in checks. Confirm the checklist items before deciding.";

        return new ApplicationReviewResult(recommendation, summary, findings, questions, "rules", null);
    }

    private static bool ExpectsArabic(ReviewField f) =>
        f.Key.EndsWith("Ar", StringComparison.Ordinal) || f.Label.Contains("Arabic", StringComparison.OrdinalIgnoreCase);

    private static bool ExpectsEnglish(ReviewField f) =>
        f.Key.EndsWith("En", StringComparison.Ordinal) || f.Label.Contains("English", StringComparison.OrdinalIgnoreCase);

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
