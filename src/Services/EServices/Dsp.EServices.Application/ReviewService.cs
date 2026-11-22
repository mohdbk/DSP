using Dsp.SharedKernel;

namespace Dsp.EServices.Application;

/// <summary>Back-office use cases: the review inbox and staff decisions.</summary>
public sealed class ReviewService(
    IReviewTaskRepository tasks,
    IApplicationRepository applications,
    IWorkflowEngine workflow,
    IServiceCatalog catalog,
    IMessages messages)
{
    /// <summary>One page of the review inbox (open tasks, oldest first unless sorted otherwise).</summary>
    public async Task<PagedList<ReviewTaskDto>> ListOpenAsync(Caller caller, ListQuery query, CancellationToken ct)
    {
        EnsureStaff(caller);
        var fields = Lists.Inbox(Lists.ServiceOptions(await catalog.ListPublishedAsync(ct)), messages);
        return (await tasks.QueryOpenAsync(fields, query, ct)).Map(ReviewTaskDto.From);
    }

    public async Task<ReviewTaskDto> GetAsync(Caller caller, Guid taskId, CancellationToken ct)
    {
        EnsureStaff(caller);
        var task = await tasks.GetAsync(taskId, ct) ?? throw new NotFoundException("errors.task.notFound");
        return ReviewTaskDto.From(task);
    }

    public async Task<ApplicationDto> DecideAsync(Caller caller, Guid taskId, DecisionRequest decision,
        CancellationToken ct)
    {
        EnsureStaff(caller);
        var task = await tasks.GetAsync(taskId, ct) ?? throw new NotFoundException("errors.task.notFound");
        var app = await applications.GetAsync(task.ApplicationId, ct)
                  ?? throw new NotFoundException("errors.application.notFound");

        app.RecordDecision(decision.Approve, decision.Comment, caller.UserId);
        task.Complete(caller.UserId, decision.Approve);
        await applications.SaveChangesAsync(ct); // one DbContext per request: saves the task too

        // Resume the workflow waiting at the staff decision step.
        await workflow.SignalStaffDecisionAsync(app.Id, ct);

        return ApplicationDto.From(await applications.ReadAsync(app.Id, ct) ?? app);
    }

    private static void EnsureStaff(Caller caller)
    {
        if (!caller.IsStaff) throw new ForbiddenException("errors.staffOnly");
    }
}
