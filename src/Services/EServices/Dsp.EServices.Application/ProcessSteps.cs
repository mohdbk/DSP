using Dsp.Contracts;
using Dsp.EServices.Domain.Applications;
using Dsp.SharedKernel;
using Microsoft.Extensions.Logging;

namespace Dsp.EServices.Application;

/// <summary>
/// The steps of the core application process. Each workflow activity calls exactly one of these,
/// so business logic stays here and the workflow only decides the order.
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
        await applications.SaveChangesAsync(ct);
        await tasks.SaveChangesAsync(ct);
        await notifier.NotifyAsync(app, "Your application is being reviewed by the ministry.", ct);
    }

    public async Task<bool> IsApprovedAsync(Guid applicationId, CancellationToken ct) =>
        (await LoadAsync(applicationId, ct)).Approved == true;

    public async Task RequestPaymentAsync(Guid applicationId, CancellationToken ct)
    {
        var app = await LoadAsync(applicationId, ct);
        var paymentId = await payments.CreatePaymentAsync(app.Id, app.ApplicantId, app.Fee, app.Currency,
            $"{app.ServiceName} – {app.Number}", ct);
        app.RequestPayment(paymentId);
        await applications.SaveChangesAsync(ct);
        await notifier.NotifyAsync(app,
            $"Your application was approved. Please pay {app.Fee:0.00} {app.Currency} to receive your certificate.", ct);
    }

    public async Task MarkPaidAsync(Guid applicationId, Guid paymentId, CancellationToken ct)
    {
        var app = await LoadAsync(applicationId, ct);
        app.MarkPaid(paymentId);
        await applications.SaveChangesAsync(ct);
        await notifier.NotifyAsync(app, "We received your payment.", ct);
    }

    public async Task IssueCertificateAsync(Guid applicationId, CancellationToken ct)
    {
        var app = await LoadAsync(applicationId, ct);
        var documentId = await certificates.IssueAsync(app, ct);
        app.Complete(documentId);
        await applications.SaveChangesAsync(ct);
        await bus.PublishAsync(new CertificateIssued(app.Id, app.Number, app.ApplicantId, documentId), ct);
        await notifier.NotifyAsync(app, "Your certificate is ready to download.", ct);
        logger.LogInformation("Issued certificate {DocumentId} for {Number}", documentId, app.Number);
    }

    public async Task RejectAsync(Guid applicationId, CancellationToken ct)
    {
        var app = await LoadAsync(applicationId, ct);
        app.Reject();
        await applications.SaveChangesAsync(ct);
        await notifier.NotifyAsync(app, $"Your application was not approved. Reason: {app.DecisionComment}", ct);
    }

    private async Task<ServiceApplication> LoadAsync(Guid id, CancellationToken ct) =>
        await applications.GetAsync(id, ct) ?? throw new NotFoundException($"Application {id} not found.");
}

/// <summary>Publishes status changes for the Notifications service.</summary>
public sealed class StatusNotifier(IEventBus bus)
{
    public Task NotifyAsync(ServiceApplication app, string message, CancellationToken ct) =>
        bus.PublishAsync(new ApplicationStatusChanged(app.Id, app.Number, app.ServiceCode, app.ServiceName,
            app.ApplicantId, app.Status.ToString(), message), ct);
}

/// <summary>Payments tells us a payment completed; record it and resume the workflow.</summary>
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

        if (app.Status != ApplicationStatus.AwaitingPayment)
        {
            logger.LogInformation("Ignoring duplicate payment event for {Number}", app.Number);
            return;
        }

        await steps.MarkPaidAsync(app.Id, @event.PaymentId, ct);
        await workflow.SignalPaymentCompletedAsync(app.Id, ct);
    }
}
