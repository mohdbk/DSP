using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Runtime.Activities;

namespace Dsp.EServices.Infrastructure.Workflows;

public static class ProcessEvents
{
    public const string ApplicationSubmitted = "dsp.application.submitted";
    public const string StaffDecision = "dsp.application.staff-decision";
    public const string PaymentCompleted = "dsp.application.payment-completed";
}

/// <summary>
/// Core process shared by every e-service (workflow version 1, the traditional journey):
/// submitted → staff review → decision → payment → certificate, or rejection.
/// The workflow instance is correlated by application ID; every step delegates to ProcessSteps.
/// </summary>
public sealed class ApplicationProcessWorkflow : WorkflowBase
{
    public const string DefinitionId = "dsp-application-process";

    protected override void Build(IWorkflowBuilder builder)
    {
        builder.WithDefinitionId(DefinitionId);
        var approved = builder.WithVariable<bool>();

        builder.Root = new Sequence
        {
            Activities =
            {
                new Event(ProcessEvents.ApplicationSubmitted) { CanStartWorkflow = true },
                new StartReview(),
                new Event(ProcessEvents.StaffDecision),
                new CheckDecision { Result = new(approved) },
                new If(context => approved.Get(context))
                {
                    Then = new Sequence
                    {
                        Activities =
                        {
                            new RequestPayment(),
                            new Event(ProcessEvents.PaymentCompleted),
                            new IssueCertificate(),
                        },
                    },
                    Else = new RejectApplication(),
                },
            },
        };
    }
}
