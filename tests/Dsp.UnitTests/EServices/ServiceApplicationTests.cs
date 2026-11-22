using Dsp.EServices.Domain.Applications;
using Dsp.EServices.Domain.Rules;
using Dsp.SharedKernel;

namespace Dsp.UnitTests.EServices;

public class ServiceApplicationTests
{
    private static ServiceApplication NewDraft(Dictionary<string, string?>? prefill = null) =>
        ServiceApplication.Create("TRADE_NAME_RESERVATION", "Trade Name Reservation", 1, "uaepass:abc", "Fatima",
            200, "AED", prefill ?? new Dictionary<string, string?>());

    private static ServiceApplication UnderReview()
    {
        var app = NewDraft();
        app.Submit("Falcon Trading");
        app.StartReview();
        return app;
    }

    [Fact]
    public void New_application_is_a_draft_with_a_number_and_prefilled_data()
    {
        var app = NewDraft(new Dictionary<string, string?> { ["fullName"] = "Fatima", ["email"] = null });

        Assert.Equal(ApplicationStatus.Draft, app.Status);
        Assert.StartsWith("DSP-", app.Number);
        Assert.Equal("Fatima", app.GetData()["fullName"]);
        Assert.False(app.GetData().ContainsKey("email")); // empty prefill values are not stored
    }

    [Fact]
    public void Draft_data_can_be_updated_and_values_are_trimmed()
    {
        var app = NewDraft();
        app.UpdateData(new Dictionary<string, string?> { ["tradeNameEn"] = "  Falcon Trading  " });

        Assert.Equal("Falcon Trading", app.GetData()["tradeNameEn"]);
    }

    [Fact]
    public void Submitted_application_can_no_longer_be_edited_or_resubmitted()
    {
        var app = NewDraft();
        app.Submit("Falcon Trading");

        Assert.Throws<DomainException>(() => app.UpdateData(new Dictionary<string, string?> { ["x"] = "y" }));
        Assert.Throws<DomainException>(() => app.Submit("Again"));
        Assert.Equal("Falcon Trading", app.ReferenceValue);
    }

    [Fact]
    public void Rejection_requires_a_reason()
    {
        var app = UnderReview();

        Assert.Throws<DomainException>(() => app.RecordDecision(false, "  ", "staff:khalid"));
        app.RecordDecision(false, "Name already in use", "staff:khalid");
        app.Reject();

        Assert.Equal(ApplicationStatus.Rejected, app.Status);
        Assert.NotNull(app.CompletedAt);
    }

    [Fact]
    public void Approved_application_goes_through_payment_to_certificate()
    {
        var app = UnderReview();
        var paymentId = Guid.NewGuid();
        var certificateId = Guid.NewGuid();

        app.RecordDecision(true, null, "staff:noura");
        app.RequestPayment(paymentId);
        Assert.Equal(ApplicationStatus.AwaitingPayment, app.Status);

        app.MarkPaid(paymentId);
        Assert.Equal(ApplicationStatus.Paid, app.Status);

        app.Complete(certificateId);
        Assert.Equal(ApplicationStatus.Approved, app.Status);
        Assert.Equal(certificateId, app.CertificateDocumentId);
    }

    [Fact]
    public void Payment_cannot_be_requested_before_approval()
    {
        var app = UnderReview();

        Assert.Throws<DomainException>(() => app.RequestPayment(Guid.NewGuid()));
    }

    [Fact]
    public void A_different_payment_cannot_mark_the_application_paid()
    {
        var app = UnderReview();
        app.RecordDecision(true, null, "staff:noura");
        app.RequestPayment(Guid.NewGuid());

        Assert.Throws<DomainException>(() => app.MarkPaid(Guid.NewGuid()));
    }

    [Fact]
    public void Only_submitted_applications_go_to_review()
    {
        Assert.Throws<DomainException>(() => NewDraft().StartReview());
    }

    [Fact]
    public void Review_task_due_date_follows_the_sla()
    {
        var app = UnderReview();
        var task = ReviewTask.For(app, TimeSpan.FromDays(2));

        Assert.Equal(ReviewTaskStatus.Open, task.Status);
        Assert.Equal(app.Number, task.ApplicationNumber);
        Assert.Equal(TimeSpan.FromDays(2), task.DueAt - task.CreatedAt);

        task.Complete("staff:khalid", approved: true);
        Assert.Equal("Approved", task.Outcome);
        Assert.Throws<DomainException>(() => task.Complete("staff:khalid", approved: false));
    }
}

public class ServiceRulesTests
{
    [Theory]
    [InlineData("Falcon Trading", true)]
    [InlineData("Federal Supplies", false)]
    [InlineData("The MINISTRY Shop", false)]
    public void Trade_names_cannot_use_reserved_words(string name, bool valid)
    {
        var errors = new TradeNameReservationRules().Validate(new Dictionary<string, string?> { ["tradeNameEn"] = name });

        Assert.Equal(valid, !errors.Any());
    }

    [Theory]
    [InlineData("2027-01-01", "2028-01-01", true)]
    [InlineData("2027-01-01", "2027-06-30", false)]
    [InlineData("2027-01-01", "2026-12-31", false)]
    public void Agency_agreements_must_run_at_least_a_year(string start, string end, bool valid)
    {
        var errors = new CommercialAgencyRules().Validate(new Dictionary<string, string?>
        {
            ["agreementStart"] = start,
            ["agreementEnd"] = end,
        });

        Assert.Equal(valid, !errors.Any());
    }
}
