using Dsp.Contracts;
using Dsp.EServices.Domain.Applications;
using Dsp.SharedKernel;
using Microsoft.Extensions.Logging;

namespace Dsp.EServices.Application;

/// <summary>
/// The steps of the core application process. Each workflow activity calls exactly one of these,
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
    ILogger<ProcessSteps> logger)
{
    public static readonly TimeSpan ReviewSla = TimeSpan.FromDays(2);

    public async Task StartReviewAsync(Guid applicationId, CancellationToken ct)
    {
        var app = await LoadAsync(applicationId, ct);
        app.StartReview();
        await tasks.AddAsync(ReviewTask.For(app, ReviewSla), ct);
        await notifier.NotifyAsync(app, "Your application is being reviewed by the ministry.", ct);
        await applications.SaveChangesAsync(ct);
    }

    public async Task<bool> IsApprovedAsync(Guid applicationId, CancellationToken ct) =>
        (await LoadAsync(applicationId, ct)).Approved == true;

    public async Task RequestPaymentAsync(Guid applicationId, CancellationToken ct)
    {
        var app = await LoadAsync(applicationId, ct);
        var paymentId = await payments.CreatePaymentAsync(app.Id, app.ApplicantId, app.Fee, app.Currency,
            $"{app.ServiceName} – {app.Number}", ct);
        app.RequestPayment(paymentId);
        await notifier.NotifyAsync(app,
            $"Your application was approved. Please pay {app.Fee:0.00} {app.Currency} to receive your certificate.", ct);
        await applications.SaveChangesAsync(ct);
    }

    public async Task MarkPaidAsync(Guid applicationId, Guid paymentId, CancellationToken ct)
    {
        var app = await LoadAsync(applicationId, ct);
        app.MarkPaid(paymentId);
        await notifier.NotifyAsync(app, "We received your payment.", ct);
        await applications.SaveChangesAsync(ct);
    }

    public async Task IssueCertificateAsync(Guid applicationId, CancellationToken ct)
    {
        var app = await LoadAsync(applicationId, ct);
        var documentId = await certificates.IssueAsync(app, ct);
        app.Complete(documentId);
        await bus.PublishAsync(new CertificateIssued(app.Id, app.Number, app.ApplicantId, documentId), ct);
        await notifier.NotifyAsync(app, "Your certificate is ready to download.", ct);
        await applications.SaveChangesAsync(ct);
        logger.LogInformation("Issued certificate {DocumentId} for {Number}", documentId, app.Number);
    }

    public async Task RejectAsync(Guid applicationId, CancellationToken ct)
    {
        var app = await LoadAsync(applicationId, ct);
        app.Reject();
        await notifier.NotifyAsync(app, $"Your application was not approved. Reason: {app.DecisionComment}", ct);
        await applications.SaveChangesAsync(ct);
    }

    private async Task<ServiceApplication> LoadAsync(Guid id, CancellationToken ct) =>
        await applications.GetAsync(id, ct) ?? throw new NotFoundException($"Application {id} not found.");
}

/// <summary>Stages status changes for the Notifications service (sent after the caller's SaveChanges).</summary>
public sealed class StatusNotifier(IEventBus bus)
{
    public Task NotifyAsync(ServiceApplication app, string message, CancellationToken ct) =>
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
