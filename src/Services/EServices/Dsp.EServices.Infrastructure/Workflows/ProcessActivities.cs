using Dsp.EServices.Application;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Attributes;

namespace Dsp.EServices.Infrastructure.Workflows;

// Thin adapters: each activity calls one ProcessSteps method. Business logic lives in the application layer.

internal static class ActivityContextExtensions
{
    public static Guid ApplicationId(this ActivityExecutionContext context) =>
        Guid.Parse(context.WorkflowExecutionContext.CorrelationId
                   ?? throw new InvalidOperationException("Workflow has no correlation ID."));

    public static ProcessSteps Steps(this ActivityExecutionContext context) =>
        context.GetRequiredService<ProcessSteps>();
}

[Activity("DSP", "Applications", "Open a staff review task.")]
public sealed class StartReview : CodeActivity
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context) =>
        await context.Steps().StartReviewAsync(context.ApplicationId(), context.CancellationToken);
}

[Activity("DSP", "Applications", "Read the staff decision.")]
public sealed class CheckDecision : CodeActivity<bool>
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var approved = await context.Steps().IsApprovedAsync(context.ApplicationId(), context.CancellationToken);
        context.Set(Result, approved);
    }
}

[Activity("DSP", "Applications", "Create the payment request.")]
public sealed class RequestPayment : CodeActivity
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context) =>
        await context.Steps().RequestPaymentAsync(context.ApplicationId(), context.CancellationToken);
}

[Activity("DSP", "Applications", "Issue the certificate.")]
public sealed class IssueCertificate : CodeActivity
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context) =>
        await context.Steps().IssueCertificateAsync(context.ApplicationId(), context.CancellationToken);
}

[Activity("DSP", "Applications", "Reject the application.")]
public sealed class RejectApplication : CodeActivity
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context) =>
        await context.Steps().RejectAsync(context.ApplicationId(), context.CancellationToken);
}

[Activity("DSP", "Applications", "Run the AI pre-review. Returns true when the applicant must be asked for more information.")]
public sealed class RunAiReview : CodeActivity<bool>
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context)
    {
        var askApplicant = await context.Steps().RunAiReviewAsync(context.ApplicationId(), context.CancellationToken);
        context.Set(Result, askApplicant);
    }
}

[Activity("DSP", "Applications", "Ask the applicant for the information the AI pre-review needs.")]
public sealed class RequestInformation : CodeActivity
{
    protected override async ValueTask ExecuteAsync(ActivityExecutionContext context) =>
        await context.Steps().RequestInformationAsync(context.ApplicationId(), context.CancellationToken);
}
