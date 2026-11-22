using Elsa.Extensions;
using Elsa.Workflows.Memory;
using Elsa.Workflows;
using Elsa.Workflows.Activities;
using Elsa.Workflows.Runtime.Activities;

namespace Dsp.EServices.Infrastructure.Workflows;

public static class ProcessEvents
{
    public const string ApplicationSubmitted = "dsp.application.submitted";
    public const string ApplicationSubmittedAiAssisted = "dsp.application.submitted.v2";
    public const string InformationProvided = "dsp.application.information-provided";
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
                DecisionPaymentAndCertificate(approved),
            },
        };
    }

    /// <summary>Everything after staff review starts. Identical in both process versions.</summary>
    internal static IActivity DecisionPaymentAndCertificate(Variable<bool> approved) => new Sequence
    {
        Activities =
        {
            new Event(ProcessEvents.StaffDecision),
            new CheckDecision { Result = new(approved) },
            new If(context => approved.Get(context))
            {
                Then = PaymentAndCertificate(),
                Else = new RejectApplication(),
            },
        },
    };

    /// <summary>After approval, by an officer or automatically.</summary>
    internal static IActivity PaymentAndCertificate() => new Sequence
    {
        Activities =
        {
            new RequestPayment(),
            new Event(ProcessEvents.PaymentCompleted),
            new IssueCertificate(),
        },
    };
}

/// <summary>
/// The AI process, for workflow versions 2 (AI-assisted) and 3 (agentic), side by side with version 1: services move to it by publishing
/// a new service version, while applications already in flight finish on the version they started.
/// submitted → AI pre-review ⟲ (ask the applicant for missing or inconsistent information, at most twice)
/// → clear case and the service allows it: approved automatically → payment → certificate;
///   otherwise: staff review with the AI's findings → decision → payment → certificate.
/// The AI never rejects; it approves only clear cases (AutoApprovalPolicy), and an audit sample still goes to staff.
/// </summary>
public sealed class AgenticApplicationProcessWorkflow : WorkflowBase
{
    public const string DefinitionId = "dsp-application-process-v2";

    protected override void Build(IWorkflowBuilder builder)
    {
        builder.WithDefinitionId(DefinitionId);
        var needsInformation = builder.WithVariable<bool>();
        var autoApproved = builder.WithVariable<bool>();
        var approved = builder.WithVariable<bool>();

        builder.Root = new Sequence
        {
            Activities =
            {
                new Event(ProcessEvents.ApplicationSubmittedAiAssisted) { CanStartWorkflow = true },
                new RunAiReview { Result = new(needsInformation) },
                new While(context => needsInformation.Get(context))
                {
                    Body = new Sequence
                    {
                        Activities =
                        {
                            new RequestInformation(),
                            new Event(ProcessEvents.InformationProvided),
                            new RunAiReview { Result = new(needsInformation) },
                        },
                    },
                },
                new TryAutoApprove { Result = new(autoApproved) },
                new If(context => autoApproved.Get(context))
                {
                    Then = ApplicationProcessWorkflow.PaymentAndCertificate(),
                    Else = new Sequence
                    {
                        Activities =
                        {
                            new StartReview(),
                            ApplicationProcessWorkflow.DecisionPaymentAndCertificate(approved),
                        },
                    },
                },
            },
        };
    }
}
