using System.Text.Json;
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
}

/// <summary>An application for one e-service, from draft to certificate.</summary>
public sealed class ServiceApplication : Entity
{
    private ServiceApplication() { }

    public string Number { get; private set; } = "";
    public string ServiceCode { get; private set; } = "";
    public string ServiceName { get; private set; } = "";
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

    public static ServiceApplication Create(string serviceCode, string serviceName, int workflowVersion,
        string applicantId, string applicantName, decimal fee, string currency,
        IReadOnlyDictionary<string, string?> prefill)
    {
        var now = DateTimeOffset.UtcNow;
        var app = new ServiceApplication
        {
            Number = $"DSP-{now:yyyy}-{Random.Shared.Next(100000, 999999)}",
            ServiceCode = serviceCode,
            ServiceName = serviceName,
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

    public void UpdateData(IReadOnlyDictionary<string, string?> values)
    {
        EnsureStatus(ApplicationStatus.Draft, "Only draft applications can be edited.");
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

    public void StartReview()
    {
        EnsureStatus(ApplicationStatus.Submitted, "Only submitted applications can go to review.");
        Status = ApplicationStatus.UnderReview;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RecordDecision(bool approved, string? comment, string decidedBy)
    {
        EnsureStatus(ApplicationStatus.UnderReview, "Only applications under review can be decided.");
        if (!approved && string.IsNullOrWhiteSpace(comment))
            throw new DomainException("A reason is required when rejecting an application.");
        Approved = approved;
        DecisionComment = comment?.Trim();
        DecidedBy = decidedBy;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RequestPayment(Guid paymentId)
    {
        if (Approved != true) throw new DomainException("Payment can only be requested after approval.");
        Status = ApplicationStatus.AwaitingPayment;
        PaymentId = paymentId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkPaid(Guid paymentId)
    {
        EnsureStatus(ApplicationStatus.AwaitingPayment, "This application is not awaiting payment.");
        if (PaymentId != paymentId) throw new DomainException("Payment does not match this application.");
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
        if (Approved != false) throw new DomainException("Only a rejection decision can reject the application.");
        Status = ApplicationStatus.Rejected;
        CompletedAt = UpdatedAt = DateTimeOffset.UtcNow;
    }

    private void EnsureStatus(ApplicationStatus expected, string message)
    {
        if (Status != expected) throw new DomainException(message);
    }
}
