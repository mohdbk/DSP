using Dsp.EServices.Domain.Catalog;

namespace Dsp.EServices.Domain.Applications;

/// <summary>Why an AI-assisted application was or wasn't approved automatically (kept in the logs and on the task).</summary>
public static class AutoApprovalReasons
{
    public const string Approved = "approved";
    public const string NotAgentic = "notAgentic";
    public const string NoModel = "noModel";
    public const string NotRecommended = "notRecommended";
    public const string Findings = "findings";
    public const string AuditSample = "auditSample";
}

public sealed record AutoApprovalOutcome(bool Approve, string Reason);

/// <summary>
/// Straight-through processing for the agentic process. The AI approves an application itself only when all of
/// these hold; otherwise a case officer decides as usual:
/// <list type="bullet">
/// <item>the service uses the agentic process (v3; AI-assisted v2 always leaves the decision to an officer);</item>
/// <item>the model reviewed it (not just the built-in checks: the AI isn't paused, missing or failing);</item>
/// <item>it recommends approval, with no questions for the applicant;</item>
/// <item>there are no issues or warnings — from the model or from the built-in checks — only "checked, fine";</item>
/// <item>the application isn't in the random audit sample (Settings → AI), which goes to an officer to keep checking the AI.</item>
/// </list>
/// The AI never rejects: a rejection always comes from an officer.
/// </summary>
public static class AutoApprovalPolicy
{
    /// <summary>Recorded as the decider of automatic approvals.</summary>
    public const string Decider = "AI";

    public static AutoApprovalOutcome Evaluate(int workflowVersion, AiAssessment? latest, Guid applicationId, int auditSamplePercent)
    {
        if (workflowVersion != WorkflowVersions.Agentic)
            return new(false, AutoApprovalReasons.NotAgentic);
        if (latest is null || latest.Mode != "llm")
            return new(false, AutoApprovalReasons.NoModel);
        if (latest.Recommendation != AiRecommendations.Approve || latest.GetQuestions().Count > 0)
            return new(false, AutoApprovalReasons.NotRecommended);
        if (latest.GetFindings().Any(f => f.Severity is "issue" or "warning"))
            return new(false, AutoApprovalReasons.Findings);
        if (InAuditSample(applicationId, auditSamplePercent))
            return new(false, AutoApprovalReasons.AuditSample);
        return new(true, AutoApprovalReasons.Approved);
    }

    /// <summary>A stable pick from the application ID, so the same application always lands the same way.</summary>
    public static bool InAuditSample(Guid applicationId, int percent)
    {
        if (percent <= 0) return false;
        if (percent >= 100) return true;
        var bytes = applicationId.ToByteArray();
        var bucket = (int)(BitConverter.ToUInt32(bytes, 0) % 100);
        return bucket < percent;
    }
}
