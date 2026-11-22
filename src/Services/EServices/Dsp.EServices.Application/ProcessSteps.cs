using Dsp.Contracts;
using Dsp.EServices.Domain.Applications;
using Dsp.EServices.Domain.Catalog;
using Dsp.SharedKernel;
using Microsoft.Extensions.Logging;

namespace Dsp.EServices.Application;

/// <summary>
/// The steps of the application process. Each workflow activity calls exactly one of these,
/// so business logic stays here and the workflow only decides the order.
/// Events are staged in the outbox before SaveChanges, so the change and its events commit together.
/// </summary>
public sealed class ProcessSteps(
    IApplicationRepository applications,
    IReviewTaskRepository tasks,
    IPaymentsGateway payments,
    ICertificateIssuer certificates,
    IEventBus bus,
    StatusNotifier notifier,
    ILogger<ProcessSteps> logger,
    IServiceCatalog catalog,
    IAiAssessmentRepository assessments,
    IAiReviewer reviewer,
    ILocalization localization,
    IAiPolicy aiPolicy,
    ICurrencies currencies)
{
    public static readonly TimeSpan ReviewSla = TimeSpan.FromDays(2);

    /// <summary>
    /// AI-assisted process: runs one AI pre-review round and records it.
    /// Returns true when the applicant should be asked for more information before staff review.
    /// If the AI is unavailable the application goes to staff as usual: the AI never blocks the process.
    /// </summary>
    public async Task<bool> RunAiReviewAsync(Guid applicationId, CancellationToken ct)
    {
        var app = await LoadAsync(applicationId, ct);
        if (app.Status == ApplicationStatus.Submitted)
        {
            app.StartAiReview();
            await notifier.NotifyAsync(app, MessageRef.Of("status.notify.aiReview"), ct);
        }

        var round = app.InformationRequestCount + 1;
        AiReviewResult result;
        try
        {
            var service = await DefinitionAsync(app, ct);
            var similar = ReferenceSimilarity.FindSimilar(app.ReferenceValue,
                await applications.ListActiveReferencesAsync(app.ServiceCode, app.Id, 500, ct));
            result = await reviewer.ReviewAsync(new AiReviewRequest(service, app.GetData(), similar, round), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "AI pre-review failed for {Number}; sending to staff review", app.Number);
            result = Unavailable(await localization.RenderAllAsync(MessageRef.Of("ai.review.unavailable"), ct));
        }

        var assessment = AiAssessment.Create(app.Id, round, result.Recommendation, result.Summary, result.Findings,
            result.Questions, result.Mode, result.Model);
        await assessments.AddAsync(assessment, ct);
        await applications.SaveChangesAsync(ct);

        var askApplicant = assessment.AsksForInformation &&
                           app.InformationRequestCount < ServiceApplication.MaxInformationRequests;
        logger.LogInformation("AI pre-review round {Round} for {Number}: {Recommendation} ({Mode}); ask applicant: {Ask}",
            round, app.Number, assessment.Recommendation, assessment.Mode, askApplicant);
        return askApplicant;
    }

    /// <summary>
    /// AI-assisted process, after the pre-review: approves a clear case automatically when the service allows it.
    /// Returns true when approved (the process goes straight to payment); false sends it to a case officer.
    /// </summary>
    public async Task<bool> TryAutoApproveAsync(Guid applicationId, CancellationToken ct)
    {
        var app = await LoadAsync(applicationId, ct);
        if (app.Status != ApplicationStatus.AiReview) return false;
        var latest = (await assessments.ListForApplicationAsync(app.Id, ct)).FirstOrDefault();
        var outcome = AutoApprovalPolicy.Evaluate(app.WorkflowVersion, latest, app.Id, await aiPolicy.GetAuditSamplePercentAsync(ct));
        logger.LogInformation("Automatic approval for {Number}: {Approve} ({Reason})", app.Number, outcome.Approve, outcome.Reason);
        if (!outcome.Approve) return false;

        app.ApproveAutomatically(); // the payment request that follows tells the applicant it was approved
        await applications.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>AI-assisted process: asks the applicant the questions from the latest pre-review.</summary>
    public async Task RequestInformationAsync(Guid applicationId, CancellationToken ct)
    {
        var app = await LoadAsync(applicationId, ct);
        var latest = (await assessments.ListForApplicationAsync(app.Id, ct)).FirstOrDefault();
        var questions = latest?.GetQuestions() ?? [];
        if (questions.Count == 0)
            questions = [await localization.RenderAllAsync(MessageRef.Of("ai.review.checkAgain"), ct)];

        app.RequestInformation(questions);
        var defaultLanguage = (await localization.GetSettingsAsync(ct)).DefaultLanguage;
        await notifier.NotifyAsync(app, MessageRef.Of("status.notify.informationRequested",
            ("questions", LocalizedText.Join(questions, " ", defaultLanguage))), ct);
        await applications.SaveChangesAsync(ct);
    }

    public async Task StartReviewAsync(Guid applicationId, CancellationToken ct)
    {
        var app = await LoadAsync(applicationId, ct);
        string? recommendation = null;
        if (WorkflowVersions.UsesAi(app.WorkflowVersion))
            recommendation = (await assessments.ListForApplicationAsync(app.Id, ct)).FirstOrDefault()?.Recommendation;

        app.StartReview();
        await tasks.AddAsync(ReviewTask.For(app, ReviewSla, recommendation), ct);
        await notifier.NotifyAsync(app, MessageRef.Of("status.notify.underReview"), ct);
        await applications.SaveChangesAsync(ct);
    }

    public async Task<bool> IsApprovedAsync(Guid applicationId, CancellationToken ct) =>
        (await LoadAsync(applicationId, ct)).Approved == true;

    public async Task RequestPaymentAsync(Guid applicationId, CancellationToken ct)
    {
        var app = await LoadAsync(applicationId, ct);
        var description = new LocalizedText(app.ServiceName.Select(n => KeyValuePair.Create(n.Key, $"{n.Value} – {app.Number}")));
        var paymentId = await payments.CreatePaymentAsync(app.Id, app.ApplicantId, app.Fee, app.Currency, description, ct);
        app.RequestPayment(paymentId);
        await notifier.NotifyAsync(app, MessageRef.Of("status.notify.approvedPayPrice",
            ("price", await currencies.FormatAsync(app.Fee, app.Currency, ct))), ct);
        await applications.SaveChangesAsync(ct);
    }

    public async Task MarkPaidAsync(Guid applicationId, Guid paymentId, CancellationToken ct)
    {
        var app = await LoadAsync(applicationId, ct);
        app.MarkPaid(paymentId);
        await notifier.NotifyAsync(app, MessageRef.Of("status.notify.paid"), ct);
        await applications.SaveChangesAsync(ct);
    }

    public async Task IssueCertificateAsync(Guid applicationId, CancellationToken ct)
    {
        var app = await LoadAsync(applicationId, ct);
        var documentId = await certificates.IssueAsync(app, ct);
        app.Complete(documentId);
        await bus.PublishAsync(new CertificateIssued(app.Id, app.Number, app.ApplicantId, documentId), ct);
        await notifier.NotifyAsync(app, MessageRef.Of("status.notify.certificateReady"), ct);
        await applications.SaveChangesAsync(ct);
        logger.LogInformation("Issued certificate {DocumentId} for {Number}", documentId, app.Number);
    }

    public async Task RejectAsync(Guid applicationId, CancellationToken ct)
    {
        var app = await LoadAsync(applicationId, ct);
        app.Reject();
        await notifier.NotifyAsync(app, MessageRef.Of("status.notify.rejected", ("reason", app.DecisionComment)), ct);
        await applications.SaveChangesAsync(ct);
    }

    private async Task<ServiceDefinition> DefinitionAsync(ServiceApplication app, CancellationToken ct) =>
        await catalog.FindVersionAsync(app.ServiceCode, app.ServiceVersion, ct)
               ?? await catalog.FindPublishedAsync(app.ServiceCode, ct)
               ?? throw new NotFoundException("errors.service.notFound");

    private static AiReviewResult Unavailable(LocalizedText summary) =>
        new(AiRecommendations.Unavailable, summary, [], [], "unavailable", null);

    private async Task<ServiceApplication> LoadAsync(Guid id, CancellationToken ct) =>
        await applications.GetAsync(id, ct) ?? throw new NotFoundException("errors.application.notFound");
}

/// <summary>
/// Stages status changes for the Notifications service (sent after the caller's SaveChanges). The message is a key
/// with arguments, so every reader sees it in their own language, including languages added later.
/// </summary>
public sealed class StatusNotifier(IEventBus bus)
{
    public Task NotifyAsync(ServiceApplication app, MessageRef message, CancellationToken ct) =>
        bus.PublishAsync(new ApplicationStatusChanged(app.Id, app.Number, app.ServiceCode, app.ServiceName,
            app.ApplicantId, app.Status.ToString(), message), ct);
}

/// <summary>
/// Payments tells us a payment completed: record it and resume the workflow.
/// Safe to run more than once: if the payment was recorded but the workflow wasn't resumed, it resumes it.
/// </summary>
public sealed class PaymentCompletedHandler(
    IApplicationRepository applications,
    ProcessSteps steps,
    IWorkflowEngine workflow,
    ILogger<PaymentCompletedHandler> logger) : IIntegrationEventHandler<PaymentCompleted>
{
    public async Task HandleAsync(PaymentCompleted @event, CancellationToken ct)
    {
        var app = await applications.ReadAsync(@event.ReferenceId, ct);
        if (app is null)
        {
            logger.LogInformation("Payment {PaymentId} is not for an e-service application", @event.PaymentId);
            return;
        }

        switch (app.Status)
        {
            case ApplicationStatus.AwaitingPayment:
                await steps.MarkPaidAsync(app.Id, @event.PaymentId, ct);
                await workflow.SignalPaymentCompletedAsync(app.Id, ct);
                break;
            case ApplicationStatus.Paid:
                // Recorded earlier but the workflow may not have resumed; resuming again is harmless.
                await workflow.SignalPaymentCompletedAsync(app.Id, ct);
                break;
            default:
                logger.LogInformation("Ignoring payment event for {Number} in status {Status}", app.Number, app.Status);
                break;
        }
    }
}
