using Dsp.Contracts;
using Dsp.EServices.Application;
using Dsp.EServices.Domain.Applications;
using Dsp.EServices.Domain.Rules;
using Dsp.EServices.Infrastructure.Catalog;
using Dsp.SharedKernel;

namespace Dsp.UnitTests.EServices;

public class ApplicationServiceTests
{
    private readonly List<string> _log = [];
    private readonly FakeApplications _apps;
    private readonly RecordingBus _bus;
    private readonly FakeWorkflow _workflow = new();
    private readonly ApplicationService _service;

    private static readonly Caller Citizen = new("uaepass:abc", "Fatima Al Mansoori", false,
        new Dictionary<string, string?>
        {
            ["name"] = "Fatima Al Mansoori",
            ["emirates_id"] = "784-1990-1234567-1",
            ["email"] = "fatima@example.ae",
            ["mobile"] = "971501234567",
        });

    public ApplicationServiceTests()
    {
        _apps = new FakeApplications(_log);
        _bus = new RecordingBus(_log);
        _service = new ApplicationService(new EmbeddedServiceCatalog(), _apps,
            [new TradeNameReservationRules(), new CommercialAgencyRules()], _workflow, new StatusNotifier(_bus));
    }

    private static Dictionary<string, string?> ValidTradeName(string name = "Falcon Trading") => new()
    {
        ["tradeNameEn"] = name,
        ["tradeNameAr"] = "الصقر للتجارة",
        ["legalForm"] = "llc",
        ["activity"] = "general-trading",
        ["emirate"] = "DXB",
        ["declaration"] = "true",
    };

    [Fact]
    public async Task Draft_is_prefilled_from_the_signed_in_profile()
    {
        var draft = await _service.CreateDraftAsync(Citizen, "TRADE_NAME_RESERVATION", default);

        Assert.Equal("Draft", draft.Status);
        Assert.Equal("Fatima Al Mansoori", draft.Data["fullName"]);
        Assert.Equal("784-1990-1234567-1", draft.Data["emiratesId"]);
        Assert.Equal("fatima@example.ae", draft.Data["email"]);
    }

    [Fact]
    public async Task Unknown_service_is_not_found()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateDraftAsync(Citizen, "NOPE", default));
    }

    [Fact]
    public async Task Saving_keeps_valid_values_and_reports_invalid_ones()
    {
        var draft = await _service.CreateDraftAsync(Citizen, "TRADE_NAME_RESERVATION", default);

        var result = await _service.SaveDataAsync(Citizen, draft.Id,
            new Dictionary<string, string?> { ["tradeNameEn"] = "Falcon Trading", ["emirate"] = "Atlantis" }, default);

        Assert.Equal("Falcon Trading", result.Application.Data["tradeNameEn"]);
        Assert.False(result.Application.Data.ContainsKey("emirate"));
        Assert.Equal("emirate", Assert.Single(result.Errors).Field);
    }

    [Fact]
    public async Task Another_users_application_is_invisible()
    {
        var draft = await _service.CreateDraftAsync(Citizen, "TRADE_NAME_RESERVATION", default);
        var stranger = Citizen with { UserId = "uaepass:someone-else" };

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(stranger, draft.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.SaveDataAsync(stranger, draft.Id, new Dictionary<string, string?>(), default));
    }

    [Fact]
    public async Task Staff_can_read_any_application()
    {
        var draft = await _service.CreateDraftAsync(Citizen, "TRADE_NAME_RESERVATION", default);
        var staff = new Caller("staff:khalid", "Khalid", true, new Dictionary<string, string?>());

        Assert.Equal(draft.Id, (await _service.GetAsync(staff, draft.Id, default)).Id);
    }

    [Fact]
    public async Task Submitting_an_incomplete_application_fails_with_field_errors()
    {
        var draft = await _service.CreateDraftAsync(Citizen, "TRADE_NAME_RESERVATION", default);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitAsync(Citizen, draft.Id, default));

        Assert.Contains(ex.Errors, e => e.Field == "tradeNameEn");
        Assert.Empty(_workflow.Started);
        Assert.Empty(_bus.Events);
    }

    [Fact]
    public async Task Business_rules_run_on_submit()
    {
        var draft = await _service.CreateDraftAsync(Citizen, "TRADE_NAME_RESERVATION", default);
        await _service.SaveDataAsync(Citizen, draft.Id, ValidTradeName("Federal Trading"), default);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitAsync(Citizen, draft.Id, default));

        Assert.Contains("reserved word", Assert.Single(ex.Errors).Message);
    }

    [Fact]
    public async Task Submit_commits_the_event_with_the_change_then_starts_the_workflow()
    {
        var draft = await _service.CreateDraftAsync(Citizen, "TRADE_NAME_RESERVATION", default);
        await _service.SaveDataAsync(Citizen, draft.Id, ValidTradeName(), default);
        _log.Clear();

        var submitted = await _service.SubmitAsync(Citizen, draft.Id, default);

        Assert.Equal("Submitted", submitted.Status);
        Assert.Equal("Falcon Trading", submitted.ReferenceValue);
        var changed = Assert.IsType<ApplicationStatusChanged>(Assert.Single(_bus.Events));
        Assert.Equal("Submitted", changed.Status);
        Assert.Equal(new[] { draft.Id }, _workflow.Started);

        // Outbox contract: the event is staged before the save that commits it.
        Assert.True(_log.IndexOf("publish:ApplicationStatusChanged") < _log.IndexOf("save"));
    }

    [Fact]
    public async Task A_reference_already_under_review_cannot_be_reserved_again()
    {
        var first = await _service.CreateDraftAsync(Citizen, "TRADE_NAME_RESERVATION", default);
        await _service.SaveDataAsync(Citizen, first.Id, ValidTradeName(), default);
        await _service.SubmitAsync(Citizen, first.Id, default);

        var second = await _service.CreateDraftAsync(Citizen, "TRADE_NAME_RESERVATION", default);
        await _service.SaveDataAsync(Citizen, second.Id, ValidTradeName(), default);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitAsync(Citizen, second.Id, default));
        Assert.Equal("tradeNameEn", Assert.Single(ex.Errors).Field);
    }

    // ---- fakes ----

    internal sealed class FakeApplications(List<string> log) : IApplicationRepository
    {
        private readonly List<ServiceApplication> _items = [];

        public Task<ServiceApplication?> GetAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(_items.FirstOrDefault(a => a.Id == id));

        public Task<ServiceApplication?> ReadAsync(Guid id, CancellationToken ct) => GetAsync(id, ct);

        public Task<IReadOnlyList<ServiceApplication>> ListForApplicantAsync(string applicantId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ServiceApplication>>(_items.Where(a => a.ApplicantId == applicantId).ToList());

        public Task<IReadOnlyList<ServiceApplication>> ListAllAsync(int take, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ServiceApplication>>(_items.Take(take).ToList());

        public Task<bool> ReferenceInUseAsync(string serviceCode, string referenceValue, Guid excludeId, CancellationToken ct) =>
            Task.FromResult(_items.Any(a => a.ServiceCode == serviceCode && a.Id != excludeId &&
                                            a.ReferenceValue == referenceValue && a.Status != ApplicationStatus.Draft &&
                                            a.Status != ApplicationStatus.Rejected));

        public Task<IReadOnlyList<string>> ListActiveReferencesAsync(string serviceCode, Guid excludeId, int take,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>(_items
                .Where(a => a.ServiceCode == serviceCode && a.Id != excludeId && a.ReferenceValue is not null)
                .Select(a => a.ReferenceValue!).Take(take).ToList());

        public Task AddAsync(ServiceApplication application, CancellationToken ct)
        {
            _items.Add(application);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct)
        {
            log.Add("save");
            return Task.CompletedTask;
        }
    }

    internal sealed class FakeWorkflow : IWorkflowEngine
    {
        public List<Guid> Started { get; } = [];
        public List<int> StartedVersions { get; } = [];
        public List<Guid> InformationProvided { get; } = [];
        public Task StartApplicationProcessAsync(Guid applicationId, int workflowVersion, CancellationToken ct)
        {
            Started.Add(applicationId);
            StartedVersions.Add(workflowVersion);
            return Task.CompletedTask;
        }
        public Task SignalInformationProvidedAsync(Guid applicationId, CancellationToken ct)
        {
            InformationProvided.Add(applicationId);
            return Task.CompletedTask;
        }
        public Task SignalStaffDecisionAsync(Guid applicationId, CancellationToken ct) => Task.CompletedTask;
        public Task SignalPaymentCompletedAsync(Guid applicationId, CancellationToken ct) => Task.CompletedTask;
    }
}

/// <summary>Event bus fake that records what was published and when.</summary>
public sealed class RecordingBus(List<string>? log = null) : IEventBus
{
    public List<IIntegrationEvent> Events { get; } = [];

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        Events.Add(@event);
        log?.Add($"publish:{typeof(TEvent).Name}");
        return Task.CompletedTask;
    }
}
