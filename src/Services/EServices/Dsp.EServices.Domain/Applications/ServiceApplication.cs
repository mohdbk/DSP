using System.Text.Json;
using Dsp.EServices.Domain.Catalog;
using Dsp.SharedKernel;

namespace Dsp.EServices.Domain.Applications;

public enum ApplicationStatus
{
    Draft,
    Submitted,
    UnderReview,
    AwaitingPayment,
    Paid,
    Approved,
    Rejected,

    /// <summary>AI-assisted process: the AI pre-review is checking the application.</summary>
    AiReview,

    /// <summary>AI-assisted process: the applicant was asked for more information and can edit and resubmit.</summary>
    InformationRequested,
}

/// <summary>An application for one e-service, from draft to certificate.</summary>
public sealed class ServiceApplication : Entity
{
    private ServiceApplication() { }

    public string Number { get; private set; } = "";
    public string ServiceCode { get; private set; } = "";
    public string ServiceName { get; private set; } = "";

    /// <summary>Version of the service definition the application started on (validation and display use it).</summary>
    public int ServiceVersion { get; private set; } = 1;

    public int WorkflowVersion { get; private set; }
    public string ApplicantId { get; private set; } = "";
    public string ApplicantName { get; private set; } = "";
    public ApplicationStatus Status { get; private set; }
    public string DataJson { get; private set; } = "{}";

    /// <summary>Value of the service's reference field (e.g. the trade name), used for lists and uniqueness.</summary>
    public string? ReferenceValue { get; private set; }

    public decimal Fee { get; private set; }
    public string Currency { get; private set; } = "AED";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public bool? Approved { get; private set; }
    public string? DecisionComment { get; private set; }
    public string? DecidedBy { get; private set; }
    public Guid? PaymentId { get; private set; }
    public Guid? CertificateDocumentId { get; private set; }

    /// <summary>Questions the applicant must answer (AI-assisted process), as JSON; null when nothing is asked.</summary>
    public string? InformationRequestJson { get; private set; }

    /// <summary>How many times the applicant was asked for more information.</summary>
    public int InformationRequestCount { get; private set; }

    /// <summary>The AI pre-review may ask the applicant this many times before handing over to staff.</summary>
    public const int MaxInformationRequests = 2;

    public static ServiceApplication Create(string serviceCode, string serviceName, int workflowVersion,
        string applicantId, string applicantName, decimal fee, string currency,
        IReadOnlyDictionary<string, string?> prefill, int serviceVersion = 1)
    {
        var now = DateTimeOffset.UtcNow;
        var app = new ServiceApplication
        {
            Number = $"DSP-{now:yyyy}-{Random.Shared.Next(100000, 999999)}",
            ServiceCode = serviceCode,
            ServiceName = serviceName,
            ServiceVersion = serviceVersion,
            WorkflowVersion = workflowVersion,
            ApplicantId = applicantId,
            ApplicantName = applicantName,
            Fee = fee,
            Currency = currency,
            Status = ApplicationStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now,
        };
        app.DataJson = JsonSerializer.Serialize(prefill.Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .ToDictionary(p => p.Key, p => p.Value));
        return app;
    }

    public Dictionary<string, string?> GetData() =>
        JsonSerializer.Deserialize<Dictionary<string, string?>>(DataJson) ?? new Dictionary<string, string?>();

    public bool IsOwnedBy(string userId) => ApplicantId == userId;

    /// <summary>Draft applications are editable, and so are applications the AI pre-review asked to complete.</summary>
    public bool IsEditable => Status is ApplicationStatus.Draft or ApplicationStatus.InformationRequested;

    public IReadOnlyList<LocalizedText> GetInformationRequest() =>
        InformationRequestJson is null
            ? []
            : JsonSerializer.Deserialize<List<LocalizedText>>(InformationRequestJson) ?? [];

    public void UpdateData(IReadOnlyDictionary<string, string?> values)
    {
        if (!IsEditable) throw new DomainException("errors.application.notEditable");
        var data = GetData();
        foreach (var (key, value) in values) data[key] = value?.Trim();
        DataJson = JsonSerializer.Serialize(data);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Submit(string? referenceValue)
    {
        EnsureStatus(ApplicationStatus.Draft, "This application has already been submitted.");
        Status = ApplicationStatus.Submitted;
        ReferenceValue = referenceValue;
        SubmittedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>AI-assisted process: the pre-review starts (or starts again after the applicant answered).</summary>
    public void StartAiReview()
    {
        EnsureStatus(ApplicationStatus.Submitted, "Only submitted applications can go to the AI pre-review.");
        Status = ApplicationStatus.AiReview;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>AI-assisted process: ask the applicant to complete or correct something before staff review.</summary>
    public void RequestInformation(IReadOnlyList<LocalizedText> questions)
    {
        EnsureStatus(ApplicationStatus.AiReview, "Information can only be requested during the AI pre-review.");
        if (questions.Count == 0) throw new DomainException("errors.application.questionsRequired");
        if (InformationRequestCount >= MaxInformationRequests)
            throw new DomainException("errors.application.maxInformationRequests");
        Status = ApplicationStatus.InformationRequested;
        InformationRequestJson = JsonSerializer.Serialize(questions);
        InformationRequestCount++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>The applicant updated the application and sent it back.</summary>
    public void ProvideInformation(string? referenceValue)
    {
        EnsureStatus(ApplicationStatus.InformationRequested, "No information was requested for this application.");
        Status = ApplicationStatus.Submitted;
        ReferenceValue = referenceValue;
        InformationRequestJson = null;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void StartReview()
    {
        if (Status is not (ApplicationStatus.Submitted or ApplicationStatus.AiReview))
            throw new DomainException("errors.application.reviewSubmittedOnly");
        Status = ApplicationStatus.UnderReview;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RecordDecision(bool approved, string? comment, string decidedBy)
    {
        EnsureStatus(ApplicationStatus.UnderReview, "Only applications under review can be decided.");
        if (!approved && string.IsNullOrWhiteSpace(comment))
            throw new DomainException("errors.application.rejectReason");
        Approved = approved;
        DecisionComment = comment?.Trim();
        DecidedBy = decidedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RequestPayment(Guid paymentId)
    {
        if (Approved != true) throw new DomainException("errors.application.paymentBeforeApproval");
        Status = ApplicationStatus.AwaitingPayment;
        PaymentId = paymentId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkPaid(Guid paymentId)
    {
        EnsureStatus(ApplicationStatus.AwaitingPayment, "This application is not awaiting payment.");
        if (PaymentId != paymentId) throw new DomainException("errors.application.paymentMismatch");
        Status = ApplicationStatus.Paid;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Complete(Guid certificateDocumentId)
    {
        EnsureStatus(ApplicationStatus.Paid, "Only paid applications can be completed.");
        Status = ApplicationStatus.Approved;
        CertificateDocumentId = certificateDocumentId;
        CompletedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Reject()
    {
        if (Approved != false) throw new DomainException("errors.application.rejectWithoutDecision");
        Status = ApplicationStatus.Rejected;
        CompletedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void EnsureStatus(ApplicationStatus expected, string message)
    {
        if (Status != expected) throw new DomainException(message);
    }
}
