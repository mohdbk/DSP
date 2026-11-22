using Dsp.EServices.Domain.Applications;
using Dsp.EServices.Domain.Catalog;

namespace Dsp.EServices.Application;

public interface IServiceCatalog
{
    IReadOnlyList<ServiceDefinition> GetAll();
    ServiceDefinition? Find(string code);
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

/// <summary>The workflow engine (Elsa) as seen by the application layer.</summary>
public interface IWorkflowEngine
{
    Task StartApplicationProcessAsync(Guid applicationId, CancellationToken ct);
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

/// <summary>The signed-in caller, as passed in from the API layer.</summary>
public sealed record Caller(string UserId, string Name, bool IsStaff, IReadOnlyDictionary<string, string?> Profile);
