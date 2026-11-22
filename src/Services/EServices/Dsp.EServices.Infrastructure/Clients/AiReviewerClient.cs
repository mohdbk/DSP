using System.Net.Http.Json;
using System.Text.Json;
using Dsp.EServices.Application;
using Dsp.EServices.Domain.Applications;
using Dsp.EServices.Domain.Catalog;

namespace Dsp.EServices.Infrastructure.Clients;

/// <summary>
/// Calls the AI Orchestrator's internal review API (not exposed through the gateway). The orchestrator owns
/// the model connection (Net0 in production) and falls back to built-in checks when no model is configured.
/// </summary>
public sealed class AiReviewerClient(HttpClient http) : IAiReviewer
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<AiReviewResult> ReviewAsync(AiReviewRequest request, CancellationToken ct)
    {
        var s = request.Service;
        var payload = new
        {
            service = new
            {
                code = s.Code,
                name = s.Name,
                nameAr = s.NameAr,
                description = s.Description,
                checklist = s.ReviewChecklist ?? [],
                fields = s.AllFields.Select(f => new
                {
                    key = f.Key,
                    label = f.Label,
                    labelAr = f.LabelAr,
                    type = f.Type,
                    required = f.Required,
                    help = f.Help,
                    options = f.Options?.Select(o => new { value = o.Value, label = o.Label }),
                }),
            },
            data = request.Data,
            similarReferences = request.SimilarReferences,
            round = request.Round,
        };

        var response = await http.PostAsJsonAsync("/internal/ai/review", payload, Json, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ReviewResponse>(Json, ct)
                   ?? throw new InvalidOperationException("The AI Orchestrator returned no review.");

        return new AiReviewResult(
            AiRecommendations.Normalize(body.Recommendation),
            body.Summary ?? "",
            (body.Findings ?? []).Select(f => new AiFinding(f.Severity ?? "info", f.Field, f.Message ?? "")).ToList(),
            (body.Questions ?? []).Where(q => !string.IsNullOrWhiteSpace(q.En))
                .Select(q => new LocalizedText(q.En!, string.IsNullOrWhiteSpace(q.Ar) ? q.En! : q.Ar!)).ToList(),
            body.Mode ?? "rules",
            body.Model);
    }

    private sealed record ReviewResponse(string? Recommendation, string? Summary, List<Finding>? Findings,
        List<Question>? Questions, string? Mode, string? Model);

    private sealed record Finding(string? Severity, string? Field, string? Message);

    private sealed record Question(string? En, string? Ar);
}
