using Dsp.EServices.Domain.Applications;
using Dsp.SharedKernel;

namespace Dsp.EServices.Application;

/// <summary>Readiness of a service for the agentic process, for the service builder (staff only).</summary>
public sealed class AiReadinessService(IApplicationRepository applications, IAiAssessmentRepository assessments)
{
    /// <summary>How many recent decisions are looked at: recent enough to reflect the current model and prompts.</summary>
    public const int Window = 500;

    public async Task<AiReadinessDto> GetAsync(Caller caller, string code, CancellationToken ct)
    {
        if (!caller.IsStaff) throw new ForbiddenException("errors.staffOnly");
        var decided = await applications.ListDecidedAsync(code, Window, ct);
        var latest = await assessments.LatestForApplicationsAsync(decided.Select(a => a.Id).ToList(), ct);
        var readiness = AiReadiness.Compute(decided.Select(a => (a, latest.GetValueOrDefault(a.Id))));
        return AiReadinessDto.From(code, readiness);
    }
}

public sealed record AiReadinessDto(string ServiceCode, int Reviewed, int Agreed, int Comparable, double? Agreement,
    int ClearCases, int ClearCasesApproved, double? ClearCaseAccuracy, int Overturned, int AutoApproved,
    int MinClearCases, double Threshold, bool Ready, bool Concern, int Window)
{
    public static AiReadinessDto From(string code, AiReadiness r) => new(code, r.Reviewed, r.Agreed, r.Comparable, r.Agreement,
        r.ClearCases, r.ClearCasesApproved, r.ClearCaseAccuracy, r.Overturned, r.AutoApproved, r.MinClearCases, r.Threshold,
        r.Ready, r.Concern, AiReadinessService.Window);
}
