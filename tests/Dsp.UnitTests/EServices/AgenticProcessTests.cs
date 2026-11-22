using Dsp.Contracts;
using Dsp.EServices.Application;
using Dsp.EServices.Domain.Applications;
using Dsp.EServices.Domain.Catalog;
using Dsp.EServices.Domain.Rules;
using Dsp.EServices.Infrastructure.Catalog;
using Dsp.SharedKernel;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dsp.UnitTests.EServices;

public class InformationRequestTests
{
    private static readonly LocalizedText Question = TestText.Text("Write the Arabic name in Arabic letters.", "اكتب الاسم بالعربية.");

    private static ServiceApplication InAiReview()
    {
        var app = ServiceApplication.Create("TRADE_NAME_RESERVATION", "Trade Name Reservation", WorkflowVersions.AiAssisted,
            "uaepass:abc", "Fatima", 200, "AED", new Dictionary<string, string?>());
        app.Submit("Falcon Trading");
        app.StartAiReview();
        return app;
    }

    [Fact]
    public void The_applicant_can_edit_and_resend_after_an_information_request()
    {
        var app = InAiReview();
        app.RequestInformation([Question]);

        Assert.Equal(ApplicationStatus.InformationRequested, app.Status);
        Assert.True(app.IsEditable);
        Assert.Equal(Question, Assert.Single(app.GetInformationRequest()));

        app.UpdateData(new Dictionary<string, string?> { ["tradeNameAr"] = "الصقر" });
        app.ProvideInformation("Falcon Trading");

        Assert.Equal(ApplicationStatus.Submitted, app.Status);
        Assert.Empty(app.GetInformationRequest());
        app.StartAiReview();
        app.StartReview();
        Assert.Equal(ApplicationStatus.UnderReview, app.Status);
    }

    [Fact]
    public void The_applicant_is_asked_at_most_twice()
    {
        var app = InAiReview();
        for (var i = 0; i < ServiceApplication.MaxInformationRequests; i++)
        {
            app.RequestInformation([Question]);
            app.ProvideInformation("Falcon Trading");
            app.StartAiReview();
        }

        Assert.Throws<DomainException>(() => app.RequestInformation([Question]));
    }

    [Fact]
    public void Information_can_only_be_requested_during_the_ai_review()
    {
        var app = ServiceApplication.Create("X", "X", 2, "u", "n", 1, "AED", new Dictionary<string, string?>());
        Assert.Throws<DomainException>(() => app.RequestInformation([Question]));
    }
}

public class AgenticProcessStepsTests
{
    private readonly List<string> _log = [];
    private readonly ApplicationServiceTests.FakeApplications _apps;
    private readonly RecordingBus _bus = new();
    private readonly InMemoryAssessments _assessments = new();
    private readonly StubReviewer _reviewer = new();
    private readonly ProcessSteps _steps;
    private readonly PaymentCompletedHandlerTests.FixedAiPolicy _policy = new(0);
    private readonly ServiceApplication _app;

    public AgenticProcessStepsTests()
    {
        _apps = new ApplicationServiceTests.FakeApplications(_log);
        _steps = new ProcessSteps(_apps, new PaymentCompletedHandlerTests.NoTasks(), new PaymentCompletedHandlerTests.NoPayments(),
            new PaymentCompletedHandlerTests.NoCertificates(), _bus, new StatusNotifier(_bus),
            NullLogger<ProcessSteps>.Instance, new EmbeddedServiceCatalog(), _assessments, _reviewer, TestText.Localization, _policy);

        _app = ServiceApplication.Create("TRADE_NAME_RESERVATION", "Trade Name Reservation", WorkflowVersions.AiAssisted,
            "uaepass:abc", "Fatima", 200, "AED", new Dictionary<string, string?> { ["tradeNameAr"] = "Falcon" });
        _app.Submit("Falcon Trading");
        _apps.AddAsync(_app, default);
    }

    [Fact]
    public async Task A_fixable_problem_sends_the_application_back_to_the_applicant()
    {
        _reviewer.Next = new AiReviewResult(AiRecommendations.RequestInformation, "Arabic name is in Latin letters.",
            [new AiFinding("issue", "tradeNameAr", "Not Arabic.")],
            [TestText.Text("Write the Arabic name in Arabic letters.", "اكتب الاسم بالعربية.")], "rules", null);

        Assert.True(await _steps.RunAiReviewAsync(_app.Id, default));
        await _steps.RequestInformationAsync(_app.Id, default);

        Assert.Equal(ApplicationStatus.InformationRequested, _app.Status);
        Assert.Equal("اكتب الاسم بالعربية.", Assert.Single(_app.GetInformationRequest())["ar"]);
        var notice = _bus.Events.OfType<ApplicationStatusChanged>().Last();
        Assert.Contains("Arabic letters", notice.Message.Render(TestText.Messages("en")));
        Assert.Contains("بالعربية", notice.Message.Render(TestText.Messages("ar")));
    }

    [Fact]
    public async Task A_clean_review_goes_to_staff_with_the_recommendation()
    {
        _reviewer.Next = new AiReviewResult(AiRecommendations.Approve, "Looks fine.", [], [], "llm", "test-model");

        Assert.False(await _steps.RunAiReviewAsync(_app.Id, default));
        Assert.Equal(ApplicationStatus.AiReview, _app.Status);
        var assessment = Assert.Single(_assessments.Items);
        Assert.Equal("llm", assessment.Mode);
        Assert.Equal(AiRecommendations.Approve, assessment.Recommendation);
    }

    [Fact]
    public async Task A_clear_case_reviewed_by_the_model_is_approved_automatically()
    {
        _reviewer.Next = new AiReviewResult(AiRecommendations.Approve, "Looks fine.",
            [new AiFinding("info", null, "Checklist item confirmed.")], [], "llm", "test-model");
        await _steps.RunAiReviewAsync(_app.Id, default);

        Assert.True(await _steps.TryAutoApproveAsync(_app.Id, default));
        Assert.True(_app.Approved);
        Assert.True(_app.DecidedByAi);
    }

    [Theory]
    [InlineData("warning", "llm", 0)]   // something for an officer to look at
    [InlineData("info", "rules", 0)]    // the model didn't review it (paused, missing or failing)
    [InlineData("info", "llm", 100)]    // in the audit sample
    public async Task Anything_short_of_a_clear_model_approval_goes_to_an_officer(string severity, string mode, int audit)
    {
        _policy.Percent = audit;
        _reviewer.Next = new AiReviewResult(AiRecommendations.Approve, "Looks fine.",
            [new AiFinding(severity, "tradeNameEn", "Check this.")], [], mode, mode == "llm" ? "test-model" : null);
        await _steps.RunAiReviewAsync(_app.Id, default);

        Assert.False(await _steps.TryAutoApproveAsync(_app.Id, default));
        Assert.Null(_app.Approved);
        Assert.Equal(ApplicationStatus.AiReview, _app.Status);
    }

    [Fact]
    public async Task The_ai_never_rejects()
    {
        _reviewer.Next = new AiReviewResult(AiRecommendations.Reject, "Imitates a brand.",
            [new AiFinding("issue", "tradeNameEn", "Brand.")], [], "llm", "test-model");
        await _steps.RunAiReviewAsync(_app.Id, default);

        Assert.False(await _steps.TryAutoApproveAsync(_app.Id, default));
        Assert.Null(_app.Approved);
    }

    [Fact]
    public void The_audit_sample_is_stable_and_roughly_the_chosen_share()
    {
        var ids = Enumerable.Range(0, 2000).Select(_ => Guid.NewGuid()).ToList();
        var share = ids.Count(id => AutoApprovalPolicy.InAuditSample(id, 10)) / 2000.0;

        Assert.InRange(share, 0.06, 0.14);
        Assert.All(ids, id => Assert.Equal(AutoApprovalPolicy.InAuditSample(id, 10), AutoApprovalPolicy.InAuditSample(id, 10)));
        Assert.DoesNotContain(ids, id => AutoApprovalPolicy.InAuditSample(id, 0));
        Assert.All(ids, id => Assert.True(AutoApprovalPolicy.InAuditSample(id, 100)));
    }

    [Fact]
    public async Task If_the_ai_is_down_the_application_still_reaches_staff()
    {
        _reviewer.Fail = true;

        Assert.False(await _steps.RunAiReviewAsync(_app.Id, default));
        Assert.Equal(AiRecommendations.Unavailable, Assert.Single(_assessments.Items).Recommendation);
    }

    [Fact]
    public async Task Similar_existing_names_are_passed_to_the_reviewer()
    {
        var other = ServiceApplication.Create("TRADE_NAME_RESERVATION", "Trade Name Reservation", 2, "uaepass:xyz", "Omar",
            200, "AED", new Dictionary<string, string?>());
        other.Submit("Falcon Trading Co");
        await _apps.AddAsync(other, default);
        _reviewer.Next = new AiReviewResult(AiRecommendations.Approve, "", [], [], "rules", null);

        await _steps.RunAiReviewAsync(_app.Id, default);

        Assert.Contains("Falcon Trading Co", _reviewer.LastRequest!.SimilarReferences);
    }
}

public class ResubmissionTests
{
    private readonly ApplicationServiceTests.FakeApplications _apps = new([]);
    private readonly ApplicationServiceTests.FakeWorkflow _workflow = new();
    private readonly ApplicationService _service;

    private static readonly Caller Citizen = new("uaepass:abc", "Fatima Al Mansoori", false,
        new Dictionary<string, string?>
        {
            ["name"] = "Fatima Al Mansoori", ["emirates_id"] = "784-1990-1234567-1",
            ["email"] = "fatima@example.ae", ["mobile"] = "971501234567",
        });

    public ResubmissionTests()
    {
        var bus = new RecordingBus();
        _service = new ApplicationService(new EmbeddedServiceCatalog(), _apps,
            [new TradeNameReservationRules(), new CommercialAgencyRules()], _workflow, new StatusNotifier(bus),
            new InMemoryAssessments(), TestText.Messages());
        _arabic = new ApplicationService(new EmbeddedServiceCatalog(), _apps,
            [new TradeNameReservationRules(), new CommercialAgencyRules()], _workflow, new StatusNotifier(bus),
            new InMemoryAssessments(), TestText.Messages("ar"));
    }

    private readonly ApplicationService _arabic;

    [Fact]
    public async Task Trade_name_reservation_runs_the_ai_assisted_process_and_resubmission_resumes_it()
    {
        var draft = await _service.CreateDraftAsync(Citizen, "TRADE_NAME_RESERVATION", default);
        await _service.SaveDataAsync(Citizen, draft.Id, new Dictionary<string, string?>
        {
            ["tradeNameEn"] = "Falcon Trading", ["tradeNameAr"] = "Falcon", ["legalForm"] = "llc",
            ["activity"] = "general-trading", ["emirate"] = "DXB", ["declaration"] = "true",
        }, default);
        await _service.SubmitAsync(Citizen, draft.Id, default);
        Assert.Equal(new[] { WorkflowVersions.AiAssisted }, _workflow.StartedVersions);

        var app = (await _apps.GetAsync(draft.Id, default))!;
        app.StartAiReview();
        app.RequestInformation([TestText.Text("Write it in Arabic.", "اكتبه بالعربية.")]);

        var saved = await _service.SaveDataAsync(Citizen, draft.Id,
            new Dictionary<string, string?> { ["tradeNameAr"] = "الصقر للتجارة" }, default);
        Assert.Empty(saved.Errors);
        var resent = await _service.SubmitAsync(Citizen, draft.Id, default);

        Assert.Equal("Submitted", resent.Status);
        Assert.Equal(new[] { draft.Id }, _workflow.InformationProvided);
        Assert.Single(_workflow.Started); // resumed, not started again
    }

    [Fact]
    public async Task Validation_messages_follow_the_callers_language()
    {
        var draft = await _arabic.CreateDraftAsync(Citizen, "TRADE_NAME_RESERVATION", default);

        var ex = await Assert.ThrowsAsync<ValidationException>(() => _arabic.SubmitAsync(Citizen, draft.Id, default));

        Assert.Contains(ex.Errors, e => e.Field == "tradeNameAr" && e.Message == "الاسم التجاري (بالعربية) مطلوب.");
    }
}

public class ReferenceSimilarityTests
{
    [Fact]
    public void Near_identical_names_are_found_and_unrelated_ones_are_not()
    {
        var similar = ReferenceSimilarity.FindSimilar("Golden Falcon Trading LLC",
            ["Golden Falcon", "Golden Falkon Est", "Blue Ocean Consultancy", "golden falcon"]);

        Assert.Contains("Golden Falcon", similar);
        Assert.Contains("Golden Falkon Est", similar);
        Assert.DoesNotContain("Blue Ocean Consultancy", similar);
        Assert.Equal(2, similar.Count); // case-insensitive duplicates count once
    }

    [Fact]
    public void Nothing_is_similar_to_an_empty_reference() =>
        Assert.Empty(ReferenceSimilarity.FindSimilar(null, ["Anything"]));
}
