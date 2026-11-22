using Dsp.EServices.Domain.Catalog;

namespace Dsp.EServices.Domain.Applications;

/// <summary>
/// How well the AI's judgement matches the officers' on one service, from decided applications that the AI reviewed.
/// Used to decide, on evidence, when an AI-assisted service can move to the agentic process (and to watch it after).
/// </summary>
/// <param name="Reviewed">Applications the AI reviewed and an officer decided.</param>
/// <param name="Agreed">Of those with an approve or reject recommendation, how many the officer decided the same way.</param>
/// <param name="Comparable">Of those officer decisions, how many had an approve or reject recommendation.</param>
/// <param name="ClearCases">Officer-decided cases the AI found clear (approve, no issues, warnings or questions): the
/// ones the agentic process would approve by itself.</param>
/// <param name="ClearCasesApproved">Clear cases the officer approved too.</param>
/// <param name="Overturned">Clear cases the officer rejected: approvals the AI would have got wrong.</param>
/// <param name="AutoApproved">Applications the AI approved itself (agentic process).</param>
public sealed record AiReadiness(int Reviewed, int Agreed, int Comparable, int ClearCases, int ClearCasesApproved,
    int Overturned, int AutoApproved, int MinClearCases, double Threshold)
{
    public double? Agreement => Comparable == 0 ? null : (double)Agreed / Comparable;
    public double? ClearCaseAccuracy => ClearCases == 0 ? null : (double)ClearCasesApproved / ClearCases;

    /// <summary>Enough clear cases seen, and officers approved almost all of them.</summary>
    public bool Ready => ClearCases >= MinClearCases && ClearCaseAccuracy >= Threshold;

    /// <summary>Officers have reviewed enough clear cases to judge, and agree with too few of them.</summary>
    public bool Concern => ClearCases >= MinClearCases && ClearCaseAccuracy < Threshold;

    public const int DefaultMinClearCases = 50;
    public const double DefaultThreshold = 0.95;

    public static AiReadiness Compute(IEnumerable<(ServiceApplication App, AiAssessment? Latest)> decided,
        int minClearCases = DefaultMinClearCases, double threshold = DefaultThreshold)
    {
        int reviewed = 0, agreed = 0, comparable = 0, clear = 0, clearApproved = 0, overturned = 0, auto = 0;
        foreach (var (app, latest) in decided)
        {
            if (!WorkflowVersions.UsesAi(app.WorkflowVersion) || app.Approved is null) continue;
            if (app.DecidedByAi) { auto++; continue; }
            if (latest is null || latest.Recommendation == AiRecommendations.Unavailable) continue;

            reviewed++;
            if (latest.Recommendation is AiRecommendations.Approve or AiRecommendations.Reject)
            {
                comparable++;
                if ((latest.Recommendation == AiRecommendations.Approve) == app.Approved) agreed++;
            }

            var isClear = latest.Mode == "llm" && latest.Recommendation == AiRecommendations.Approve
                          && latest.GetQuestions().Count == 0
                          && !latest.GetFindings().Any(f => f.Severity is "issue" or "warning");
            if (!isClear) continue;
            clear++;
            if (app.Approved == true) clearApproved++; else overturned++;
        }
        return new AiReadiness(reviewed, agreed, comparable, clear, clearApproved, overturned, auto, minClearCases, threshold);
    }
}
