using Dsp.SharedKernel;

namespace Dsp.EServices.Domain.Applications;

public enum ReviewTaskStatus
{
    Open,
    Completed,
}

/// <summary>A back-office task created by the workflow when an application needs a staff decision.</summary>
public sealed class ReviewTask : Entity
{
    private ReviewTask() { }

    public Guid ApplicationId { get; private set; }
    public string ApplicationNumber { get; private set; } = "";
    public string ServiceCode { get; private set; } = "";
    public string ServiceName { get; private set; } = "";
    public string ApplicantName { get; private set; } = "";
    public string? ReferenceValue { get; private set; }
    public ReviewTaskStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset DueAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? CompletedBy { get; private set; }
    public string? Outcome { get; private set; }

    /// <summary>The AI pre-review's recommendation (AI-assisted process), shown to staff. Staff always decide.</summary>
    public string? AiRecommendation { get; private set; }

    public static ReviewTask For(ServiceApplication application, TimeSpan sla, string? aiRecommendation = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new ReviewTask
        {
            ApplicationId = application.Id,
            ApplicationNumber = application.Number,
            ServiceCode = application.ServiceCode,
            ServiceName = application.ServiceName,
            ApplicantName = application.ApplicantName,
            ReferenceValue = application.ReferenceValue,
            Status = ReviewTaskStatus.Open,
            CreatedAt = now,
            DueAt = now.Add(sla),
            AiRecommendation = aiRecommendation,
        };
    }

    public void Complete(string staffId, bool approved)
    {
        if (Status != ReviewTaskStatus.Open) throw new DomainException("errors.task.completed");
        Status = ReviewTaskStatus.Completed;
        CompletedBy = staffId;
        CompletedAt = DateTimeOffset.UtcNow;
        Outcome = approved ? "Approved" : "Rejected";
    }
}
