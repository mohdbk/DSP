using Dsp.EServices.Application;
using Dsp.EServices.Domain.Applications;

namespace Dsp.UnitTests.EServices;

/// <summary>AI pre-review records kept in memory, newest round first.</summary>
public sealed class InMemoryAssessments : IAiAssessmentRepository
{
    public List<AiAssessment> Items { get; } = [];

    public Task<IReadOnlyList<AiAssessment>> ListForApplicationAsync(Guid applicationId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<AiAssessment>>(Items.Where(a => a.ApplicationId == applicationId)
            .OrderByDescending(a => a.Round).ToList());

    public Task<IReadOnlyDictionary<Guid, AiAssessment>> LatestForApplicationsAsync(IReadOnlyCollection<Guid> applicationIds,
        CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<Guid, AiAssessment>>(Items.Where(a => applicationIds.Contains(a.ApplicationId))
            .GroupBy(a => a.ApplicationId).ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.Round).First()));

    public Task AddAsync(AiAssessment assessment, CancellationToken ct)
    {
        Items.Add(assessment);
        return Task.CompletedTask;
    }
}

/// <summary>An AI reviewer that returns a fixed answer, or fails.</summary>
public sealed class StubReviewer : IAiReviewer
{
    public AiReviewResult Next { get; set; } = new(AiRecommendations.Approve, "", [], [], "rules", null);
    public bool Fail { get; set; }
    public AiReviewRequest? LastRequest { get; private set; }

    public Task<AiReviewResult> ReviewAsync(AiReviewRequest request, CancellationToken ct)
    {
        LastRequest = request;
        if (Fail) throw new HttpRequestException("AI Orchestrator unavailable");
        return Task.FromResult(Next);
    }
}
