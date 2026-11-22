using Dsp.EServices.Domain.Applications;
using Dsp.EServices.Domain.Catalog;

namespace Dsp.EServices.Application;

/// <summary>Published service definitions, as citizens and the process see them.</summary>
public interface IServiceCatalog
{
    Task<IReadOnlyList<ServiceDefinition>> ListPublishedAsync(CancellationToken ct);
    Task<ServiceDefinition?> FindPublishedAsync(string code, CancellationToken ct);

    /// <summary>A specific version, published or retired (applications keep the version they started on).</summary>
    Task<ServiceDefinition?> FindVersionAsync(string code, int version, CancellationToken ct);
}

/// <summary>All versions of all services, for the service builder.</summary>
public interface IServiceVersionRepository
{
    Task<IReadOnlyList<ServiceVersion>> ListAllAsync(CancellationToken ct);
    Task<IReadOnlyList<ServiceVersion>> ListForCodeAsync(string code, CancellationToken ct);
    Task<bool> AnyAsync(CancellationToken ct);
    Task AddAsync(ServiceVersion version, CancellationToken ct);
    void Remove(ServiceVersion version);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IApplicationRepository
{
    /// <summary>Tracked, for changes.</summary>
    Task<ServiceApplication?> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Untracked, always reflects the latest committed state.</summary>
    Task<ServiceApplication?> ReadAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<ServiceApplication>> ListForApplicantAsync(string applicantId, CancellationToken ct);
    Task<IReadOnlyList<ServiceApplication>> ListAllAsync(int take, CancellationToken ct);
    Task<bool> ReferenceInUseAsync(string serviceCode, string referenceValue, Guid excludeId, CancellationToken ct);

    /// <summary>Reference values (e.g. trade names) of other active applications for the service.</summary>
    Task<IReadOnlyList<string>> ListActiveReferencesAsync(string serviceCode, Guid excludeId, int take, CancellationToken ct);

    Task AddAsync(ServiceApplication application, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IReviewTaskRepository
{
    Task<ReviewTask?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ReviewTask>> ListOpenAsync(CancellationToken ct);
    Task<IReadOnlyList<ReviewTask>> ListForApplicationAsync(Guid applicationId, CancellationToken ct);
    Task AddAsync(ReviewTask task, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface IAiAssessmentRepository
{
    Task<IReadOnlyList<AiAssessment>> ListForApplicationAsync(Guid applicationId, CancellationToken ct);
    Task AddAsync(AiAssessment assessment, CancellationToken ct);
}

/// <summary>The workflow engine (Elsa) as seen by the application layer.</summary>
public interface IWorkflowEngine
{
    /// <summary>Starts the process version the application was created with (1 standard, 2 AI-assisted).</summary>
    Task StartApplicationProcessAsync(Guid applicationId, int workflowVersion, CancellationToken ct);
    Task SignalInformationProvidedAsync(Guid applicationId, CancellationToken ct);
    Task SignalStaffDecisionAsync(Guid applicationId, CancellationToken ct);
    Task SignalPaymentCompletedAsync(Guid applicationId, CancellationToken ct);
}

/// <summary>Payments common service.</summary>
public interface IPaymentsGateway
{
    Task<Guid> CreatePaymentAsync(Guid applicationId, string payerId, decimal amount, string currency,
        string description, CancellationToken ct);
}

/// <summary>Produces and stores the certificate document for a completed application.</summary>
public interface ICertificateIssuer
{
    Task<Guid> IssueAsync(ServiceApplication application, CancellationToken ct);
}

/// <summary>What the AI pre-review is given: the service, the answers and context it can't see by itself.</summary>
public sealed record AiReviewRequest(
    ServiceDefinition Service,
    IReadOnlyDictionary<string, string?> Data,
    IReadOnlyList<string> SimilarReferences,
    int Round);

public sealed record AiReviewResult(
    string Recommendation,
    string Summary,
    IReadOnlyList<AiFinding> Findings,
    IReadOnlyList<LocalizedText> Questions,
    string Mode,
    string? Model);

/// <summary>The AI pre-review (the AI Orchestrator service). Advice only: staff make every decision.</summary>
public interface IAiReviewer
{
    Task<AiReviewResult> ReviewAsync(AiReviewRequest request, CancellationToken ct);
}

/// <summary>The signed-in caller, as passed in from the API layer.</summary>
public sealed record Caller(string UserId, string Name, bool IsStaff, IReadOnlyDictionary<string, string?> Profile,
    string Language = Localized.English);
