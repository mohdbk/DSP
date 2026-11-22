using Dsp.SharedKernel;
using Dsp.Contracts;
using Dsp.EServices.Application;
using Dsp.EServices.Domain.Applications;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dsp.UnitTests.EServices;

public class PaymentCompletedHandlerTests
{
    private readonly Store _store = new();
    private readonly Workflow _workflow = new();
    private readonly RecordingBus _bus = new();
    private readonly PaymentCompletedHandler _handler;

    public PaymentCompletedHandlerTests()
    {
        var steps = new ProcessSteps(_store, new NoTasks(), new NoPayments(), new NoCertificates(), _bus,
            new StatusNotifier(_bus), NullLogger<ProcessSteps>.Instance, new Dsp.EServices.Infrastructure.Catalog.EmbeddedServiceCatalog(),
            new InMemoryAssessments(), new StubReviewer(), TestText.Localization, new FixedAiPolicy(100), TestText.Currencies);
        _handler = new PaymentCompletedHandler(_store, steps, _workflow, NullLogger<PaymentCompletedHandler>.Instance);
    }

    private ServiceApplication AwaitingPayment(Guid paymentId)
    {
        var app = ServiceApplication.Create("SVC", "Service", 1, "uaepass:abc", "Fatima", 200, "AED",
            new Dictionary<string, string?>());
        app.Submit(null);
        app.StartReview();
        app.RecordDecision(true, null, "staff:khalid");
        app.RequestPayment(paymentId);
        _store.Item = app;
        return app;
    }

    private static PaymentCompleted Paid(Guid paymentId, Guid applicationId) =>
        new(paymentId, applicationId, "uaepass:abc", 200, "AED");

    [Fact]
    public async Task Records_payment_and_resumes_the_workflow()
    {
        var paymentId = Guid.NewGuid();
        var app = AwaitingPayment(paymentId);

        await _handler.HandleAsync(Paid(paymentId, app.Id), default);

        Assert.Equal(ApplicationStatus.Paid, app.Status);
        Assert.Equal(new[] { app.Id }, _workflow.Resumed);
        Assert.Contains(_bus.Events, e => e is ApplicationStatusChanged { Status: "Paid" });
    }

    [Fact]
    public async Task Redelivery_after_partial_failure_resumes_without_paying_twice()
    {
        var paymentId = Guid.NewGuid();
        var app = AwaitingPayment(paymentId);
        await _handler.HandleAsync(Paid(paymentId, app.Id), default);
        var eventsAfterFirst = _bus.Events.Count;

        await _handler.HandleAsync(Paid(paymentId, app.Id), default);

        Assert.Equal(ApplicationStatus.Paid, app.Status);
        Assert.Equal(2, _workflow.Resumed.Count);
        Assert.Equal(eventsAfterFirst, _bus.Events.Count);
    }

    [Fact]
    public async Task Payments_for_other_references_are_ignored()
    {
        await _handler.HandleAsync(Paid(Guid.NewGuid(), Guid.NewGuid()), default);

        Assert.Empty(_workflow.Resumed);
    }

    private sealed class Store : IApplicationRepository
    {
        public ServiceApplication? Item { get; set; }
        public Task<ServiceApplication?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(Item?.Id == id ? Item : null);
        public Task<ServiceApplication?> ReadAsync(Guid id, CancellationToken ct) => GetAsync(id, ct);
        public Task<IReadOnlyList<ServiceApplication>> ListForApplicantAsync(string applicantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ServiceApplication>> ListAllAsync(int take, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ServiceApplication>> ListDecidedAsync(string serviceCode, int take, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> ReferenceInUseAsync(string s, string r, Guid e, CancellationToken ct) => Task.FromResult(false);
        public Task<IReadOnlyList<string>> ListActiveReferencesAsync(string s, Guid e, int take, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([]);
        public Task AddAsync(ServiceApplication application, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class Workflow : IWorkflowEngine
    {
        public List<Guid> Resumed { get; } = [];
        public Task StartApplicationProcessAsync(Guid id, int version, CancellationToken ct) => Task.CompletedTask;
        public Task SignalInformationProvidedAsync(Guid id, CancellationToken ct) => Task.CompletedTask;
        public Task SignalStaffDecisionAsync(Guid id, CancellationToken ct) => Task.CompletedTask;
        public Task SignalPaymentCompletedAsync(Guid id, CancellationToken ct) { Resumed.Add(id); return Task.CompletedTask; }
    }

    internal sealed class FixedAiPolicy(int auditSamplePercent) : IAiPolicy
    {
        public int Percent { get; set; } = auditSamplePercent;
        public Task<int> GetAuditSamplePercentAsync(CancellationToken ct) => Task.FromResult(Percent);
    }

    internal sealed class NoTasks : IReviewTaskRepository
    {
        public Task<ReviewTask?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult<ReviewTask?>(null);
        public Task<IReadOnlyList<ReviewTask>> ListOpenAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ReviewTask>>([]);
        public Task<IReadOnlyList<ReviewTask>> ListForApplicationAsync(Guid id, CancellationToken ct) => Task.FromResult<IReadOnlyList<ReviewTask>>([]);
        public Task AddAsync(ReviewTask task, CancellationToken ct) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct) => Task.CompletedTask;
    }

    internal sealed class NoPayments : IPaymentsGateway
    {
        public Task<Guid> CreatePaymentAsync(Guid a, string p, decimal amount, string c, LocalizedText d, CancellationToken ct) =>
            Task.FromResult(Guid.NewGuid());
    }

    internal sealed class NoCertificates : ICertificateIssuer
    {
        public Task<Guid> IssueAsync(ServiceApplication application, CancellationToken ct) => Task.FromResult(Guid.NewGuid());
    }
}
