using System.Text.Json;
using Dsp.EServices.Domain.Catalog;
using Dsp.SharedKernel;

namespace Dsp.EServices.Domain.Applications;

public static class AiRecommendations
{
    public const string Approve = "approve";
    public const string RequestInformation = "request_information";
    public const string Reject = "reject";

    /// <summary>The pre-review could not run (e.g. the AI service was unavailable); staff review as usual.</summary>
    public const string Unavailable = "unavailable";

    public static string Normalize(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        Approve => Approve,
        RequestInformation or "request_info" or "needs_information" => RequestInformation,
        Reject => Reject,
        _ => Unavailable,
    };
}

/// <summary>One finding of the AI pre-review, in every enabled language. Severity is "info", "warning" or "issue".</summary>
public sealed record AiFinding(string Severity, string? Field, LocalizedText Message);

/// <summary>
/// The result of one AI pre-review round, kept for audit. It is advice: the AI can ask the applicant for
/// missing or inconsistent information, but only staff approve or reject.
/// </summary>
public sealed class AiAssessment : Entity
{
    private AiAssessment() { }

    public Guid ApplicationId { get; private set; }
    public int Round { get; private set; }
    public string Recommendation { get; private set; } = AiRecommendations.Unavailable;
    /// <summary>The summary in every enabled language, stored as a JSON map .</summary>
    public string Summary { get; private set; } = "";
    public string FindingsJson { get; private set; } = "[]";
    public string QuestionsJson { get; private set; } = "[]";

    /// <summary>"llm" when a model reviewed it, "rules" for the built-in checks, "unavailable" if neither ran.</summary>
    public string Mode { get; private set; } = "rules";

    public string? Model { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    public static AiAssessment Create(Guid applicationId, int round, string recommendation, LocalizedText summary,
        IReadOnlyList<AiFinding> findings, IReadOnlyList<LocalizedText> questions, string mode, string? model)
    {
        if (round < 1) throw new DomainException("errors.assessment.round");
        return new AiAssessment
        {
            ApplicationId = applicationId,
            Round = round,
            Recommendation = AiRecommendations.Normalize(recommendation),
            Summary = Truncate(summary.ToJson(), 4000),
            FindingsJson = JsonSerializer.Serialize(findings.Take(50).ToList()),
            QuestionsJson = JsonSerializer.Serialize(questions.Take(10).ToList()),
            Mode = mode,
            Model = model is null ? null : Truncate(model, 100),
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public LocalizedText GetSummary() => LocalizedText.Parse(Summary);

    public IReadOnlyList<AiFinding> GetFindings() =>
        JsonSerializer.Deserialize<List<AiFinding>>(FindingsJson) ?? [];

    public IReadOnlyList<LocalizedText> GetQuestions() =>
        JsonSerializer.Deserialize<List<LocalizedText>>(QuestionsJson) ?? [];

    public bool AsksForInformation => Recommendation == AiRecommendations.RequestInformation && GetQuestions().Count > 0;

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
