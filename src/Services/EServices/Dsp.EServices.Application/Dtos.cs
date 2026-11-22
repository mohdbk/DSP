using Dsp.EServices.Domain.Applications;
using Dsp.EServices.Domain.Catalog;

namespace Dsp.EServices.Application;

public sealed record ServiceSummaryDto(string Code, string Name, string? NameAr, string Description, string Category,
    decimal Fee, string Currency, int StepCount)
{
    public static ServiceSummaryDto From(ServiceDefinition d) =>
        new(d.Code, d.Name, d.NameAr, d.Description, d.Category, d.Fee, d.Currency, d.Steps.Count);
}

public sealed record ApplicationDto(
    Guid Id,
    string Number,
    string ServiceCode,
    string ServiceName,
    int WorkflowVersion,
    string ApplicantId,
    string ApplicantName,
    string Status,
    Dictionary<string, string?> Data,
    string? ReferenceValue,
    decimal Fee,
    string Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? CompletedAt,
    bool? Approved,
    string? DecisionComment,
    Guid? PaymentId,
    Guid? CertificateDocumentId)
{
    public static ApplicationDto From(ServiceApplication a) => new(a.Id, a.Number, a.ServiceCode, a.ServiceName,
        a.WorkflowVersion, a.ApplicantId, a.ApplicantName, a.Status.ToString(), a.GetData(), a.ReferenceValue, a.Fee,
        a.Currency, a.CreatedAt, a.UpdatedAt, a.SubmittedAt, a.CompletedAt, a.Approved, a.DecisionComment,
        a.PaymentId, a.CertificateDocumentId);
}

public sealed record ReviewTaskDto(Guid Id, Guid ApplicationId, string ApplicationNumber, string ServiceCode,
    string ServiceName, string ApplicantName, string? ReferenceValue, string Status, DateTimeOffset CreatedAt,
    DateTimeOffset DueAt, DateTimeOffset? CompletedAt, string? Outcome)
{
    public static ReviewTaskDto From(ReviewTask t) => new(t.Id, t.ApplicationId, t.ApplicationNumber, t.ServiceCode,
        t.ServiceName, t.ApplicantName, t.ReferenceValue, t.Status.ToString(), t.CreatedAt, t.DueAt, t.CompletedAt,
        t.Outcome);
}

public sealed record SaveResultDto(ApplicationDto Application, IReadOnlyList<Dsp.SharedKernel.FieldError> Errors);

public sealed record SaveDataRequest(Dictionary<string, string?> Values);

public sealed record CreateApplicationRequest(string ServiceCode);

public sealed record DecisionRequest(bool Approve, string? Comment);
