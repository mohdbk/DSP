namespace Dsp.Contracts;

/// <summary>An application moved to a new status. Consumed by Notifications and Reporting.</summary>
public sealed record ApplicationStatusChanged(
    Guid ApplicationId,
    string ApplicationNumber,
    string ServiceCode,
    string ServiceName,
    string ApplicantId,
    string Status,
    string Message,
    string? MessageAr = null) : IntegrationEvent;

/// <summary>A payment was completed by the payer. Consumed by E-Services to resume the workflow.</summary>
public sealed record PaymentCompleted(
    Guid PaymentId,
    Guid ReferenceId,
    string PayerId,
    decimal Amount,
    string Currency) : IntegrationEvent;

/// <summary>A certificate document was issued for an application.</summary>
public sealed record CertificateIssued(
    Guid ApplicationId,
    string ApplicationNumber,
    string ApplicantId,
    Guid DocumentId) : IntegrationEvent;
