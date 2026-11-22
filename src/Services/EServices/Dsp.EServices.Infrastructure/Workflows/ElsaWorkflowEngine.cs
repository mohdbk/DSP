using Dsp.EServices.Application;
using Dsp.EServices.Domain.Catalog;
using Elsa.Workflows.Runtime;
using Microsoft.Extensions.Logging;

namespace Dsp.EServices.Infrastructure.Workflows;

/// <summary>Starts and resumes the application process by publishing correlated Elsa events.</summary>
public sealed class ElsaWorkflowEngine(IEventPublisher events, ILogger<ElsaWorkflowEngine> logger) : IWorkflowEngine
{
    public Task StartApplicationProcessAsync(Guid applicationId, int workflowVersion, CancellationToken ct) =>
        PublishAsync(workflowVersion == WorkflowVersions.AiAssisted
            ? ProcessEvents.ApplicationSubmittedAiAssisted
            : ProcessEvents.ApplicationSubmitted, applicationId, ct);

    public Task SignalInformationProvidedAsync(Guid applicationId, CancellationToken ct) =>
        PublishAsync(ProcessEvents.InformationProvided, applicationId, ct);

    public Task SignalStaffDecisionAsync(Guid applicationId, CancellationToken ct) =>
        PublishAsync(ProcessEvents.StaffDecision, applicationId, ct);

    public Task SignalPaymentCompletedAsync(Guid applicationId, CancellationToken ct) =>
        PublishAsync(ProcessEvents.PaymentCompleted, applicationId, ct);

    private async Task PublishAsync(string eventName, Guid applicationId, CancellationToken ct)
    {
        logger.LogInformation("Workflow event {Event} for application {ApplicationId}", eventName, applicationId);
        await events.PublishAsync(eventName, applicationId.ToString(), null, null, null, false, ct);
    }
}
