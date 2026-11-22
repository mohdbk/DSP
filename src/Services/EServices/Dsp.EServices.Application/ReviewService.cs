using Dsp.SharedKernel;

namespace Dsp.EServices.Application;

/// <summary>Back-office use cases: the review inbox and staff decisions.</summary>
public sealed class ReviewService(
    IReviewTaskRepository tasks,
    IApplicationRepository applications,
    IWorkflowEngine workflow)
{
    public async Task<IReadOnlyList<ReviewTaskDto>> ListOpenAsync(Caller caller, CancellationToken ct)
    {
        EnsureStaff(caller);
        return (await tasks.ListOpenAsync(ct)).Select(ReviewTaskDto.From).ToList();
    }

    public async Task<ReviewTaskDto> GetAsync(Caller caller, Guid taskId, CancellationToken ct)
    {
        EnsureStaff(caller);
        var task = await tasks.GetAsync(taskId, ct) ?? throw new NotFoundException("Task not found.");
        return ReviewTaskDto.From(task);
    }

    public async Task<ApplicationDto> DecideAsync(Caller caller, Guid taskId, DecisionRequest decision,
        CancellationToken ct)
    {
        EnsureStaff(caller);
        var task = await tasks.GetAsync(taskId, ct) ?? throw new NotFoundException("Task not found.");
        var app = await applications.GetAsync(task.ApplicationId, ct)
                  ?? throw new NotFoundException("Application not found.");

        app.RecordDecision(decision.Approve, decision.Comment, caller.UserId);
        task.Complete(caller.UserId, decision.Approve);
        await applications.SaveChangesAsync(ct); // one DbContext per request: saves the task too

        // Resume the workflow waiting at the staff decision step.
        await workflow.SignalStaffDecisionAsync(app.Id, ct);

        return ApplicationDto.From(await applications.ReadAsync(app.Id, ct) ?? app);
    }

    private static void EnsureStaff(Caller caller)
    {
        if (!caller.IsStaff) throw new ForbiddenException("Back-office access only.");
    }
}
