using Dsp.SharedKernel;

namespace Dsp.Contracts;

/// <summary>
/// An application moved to a new status. Consumed by Notifications and Reporting. The service name comes in every
/// language it has, and the message is a key plus arguments, so each reader sees it in their own language.
/// </summary>
public sealed record ApplicationStatusChanged(
    Guid ApplicationId,
    string ApplicationNumber,
    string ServiceCode,
    LocalizedText ServiceName,
    string ApplicantId,
    string Status,
    MessageRef Message) : IntegrationEvent;

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
