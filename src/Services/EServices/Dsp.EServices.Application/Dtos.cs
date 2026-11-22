using Dsp.EServices.Domain.Applications;
using Dsp.EServices.Domain.Catalog;
using Dsp.SharedKernel;

namespace Dsp.EServices.Application;

public sealed record ServiceSummaryDto(string Code, string Name, string? NameAr, string Description, string? DescriptionAr,
    string Category, string? CategoryAr, decimal Fee, string Currency, int StepCount, int Version, int WorkflowVersion)
{
    public static ServiceSummaryDto From(ServiceDefinition d) =>
        new(d.Code, d.Name, d.NameAr, d.Description, d.DescriptionAr, d.Category, d.CategoryAr, d.Fee, d.Currency,
            d.Steps.Count, d.Version, d.WorkflowVersion);
}

public sealed record ApplicationDto(
    Guid Id,
    string Number,
    string ServiceCode,
    string ServiceName,
    int ServiceVersion,
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
    Guid? CertificateDocumentId,
    IReadOnlyList<LocalizedText> InformationRequest,
    int InformationRequestCount)
{
    public static ApplicationDto From(ServiceApplication a) => new(a.Id, a.Number, a.ServiceCode, a.ServiceName,
        a.ServiceVersion, a.WorkflowVersion, a.ApplicantId, a.ApplicantName, a.Status.ToString(), a.GetData(),
        a.ReferenceValue, a.Fee, a.Currency, a.CreatedAt, a.UpdatedAt, a.SubmittedAt, a.CompletedAt, a.Approved,
        a.DecisionComment, a.PaymentId, a.CertificateDocumentId, a.GetInformationRequest(), a.InformationRequestCount);
}

public sealed record ReviewTaskDto(Guid Id, Guid ApplicationId, string ApplicationNumber, string ServiceCode,
    string ServiceName, string ApplicantName, string? ReferenceValue, string Status, DateTimeOffset CreatedAt,
    DateTimeOffset DueAt, DateTimeOffset? CompletedAt, string? Outcome, string? AiRecommendation)
{
    public static ReviewTaskDto From(ReviewTask t) => new(t.Id, t.ApplicationId, t.ApplicationNumber, t.ServiceCode,
        t.ServiceName, t.ApplicantName, t.ReferenceValue, t.Status.ToString(), t.CreatedAt, t.DueAt, t.CompletedAt,
        t.Outcome, t.AiRecommendation);
}

public sealed record AiAssessmentDto(Guid Id, int Round, string Recommendation, string Summary,
    IReadOnlyList<AiFinding> Findings, IReadOnlyList<LocalizedText> Questions, string Mode, string? Model,
    DateTimeOffset CreatedAt)
{
    public static AiAssessmentDto From(AiAssessment a) => new(a.Id, a.Round, a.Recommendation, a.Summary,
        a.GetFindings(), a.GetQuestions(), a.Mode, a.Model, a.CreatedAt);
}

public sealed record SaveResultDto(ApplicationDto Application, IReadOnlyList<FieldError> Errors);

public sealed record SaveDataRequest(Dictionary<string, string?> Values);

public sealed record CreateApplicationRequest(string ServiceCode);

public sealed record DecisionRequest(bool Approve, string? Comment);

// Service builder

public sealed record BuilderServiceSummaryDto(string Code, string Name, string? NameAr, string Category,
    int? PublishedVersion, int? DraftVersion, int WorkflowVersion, DateTimeOffset UpdatedAt, string UpdatedBy);

public sealed record ServiceVersionDto(int Version, string Status, DateTimeOffset CreatedAt, string CreatedBy,
    DateTimeOffset UpdatedAt, string UpdatedBy, DateTimeOffset? PublishedAt, string? PublishedBy)
{
    public static ServiceVersionDto From(ServiceVersion v) => new(v.Version, v.Status.ToString(), v.CreatedAt,
        v.CreatedBy, v.UpdatedAt, v.UpdatedBy, v.PublishedAt, v.PublishedBy);
}

public sealed record BuilderServiceDto(string Code, ServiceDefinition? Published, ServiceDefinition? Draft,
    IReadOnlyList<FieldError> DraftErrors, IReadOnlyList<ServiceVersionDto> Versions);
