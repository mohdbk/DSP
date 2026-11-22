using Dsp.EServices.Domain.Applications;
using Dsp.EServices.Domain.Catalog;
using Dsp.SharedKernel;

namespace Dsp.EServices.Application;

/// <summary>
/// The searchable, filterable and sortable columns of each E-Services list. Column ids match the tables in the portals.
/// The rules run in the database for applications and inbox tasks.
/// </summary>
public static class Lists
{
    /// <summary>Services by code with their names, so the service filter shows names and search finds a service by name.</summary>
    public static IReadOnlyList<ListOption> ServiceOptions(IEnumerable<ServiceDefinition> services) =>
        services.GroupBy(s => s.Code, StringComparer.OrdinalIgnoreCase).Select(g => new ListOption(g.Key, g.First().Name))
            .OrderBy(o => o.Value, StringComparer.OrdinalIgnoreCase).ToList();

    public static IReadOnlyList<ListOption> Labelled(IMessages messages, string prefix, IEnumerable<string> values) =>
        values.Select(v => new ListOption(v, LocalizedText.Of(messages.Language, messages.Format(prefix + v)))).ToList();

    public static IReadOnlyList<ListOption> ProcessOptions(IMessages messages) =>
        WorkflowVersions.All.Select(v => new ListOption(v.ToString(System.Globalization.CultureInfo.InvariantCulture),
            LocalizedText.Of(messages.Language, messages.Format($"process.v{v}")))).ToList();

    /// <summary>Applications: all of them for staff, or one applicant's own.</summary>
    public static ListFields<ServiceApplication> Applications(IReadOnlyList<ListOption> services, IMessages messages) =>
        new ListFields<ServiceApplication>(q => q.OrderByDescending(a => a.UpdatedAt).ThenByDescending(a => a.Number))
            .Text("number", a => a.Number)
            .Choice("service", a => a.ServiceCode, services)
            .Text("applicant", a => a.ApplicantName)
            .Text("reference", a => a.ReferenceValue)
            .Int("process", a => a.WorkflowVersion, ProcessOptions(messages))
            .Enum("status", a => a.Status, Labelled(messages, "status.", Enum.GetNames<ApplicationStatus>()))
            .Number("fee", a => a.Fee)
            .Date("created", a => a.CreatedAt)
            .Date("submitted", a => a.SubmittedAt)
            .Date("updated", a => a.UpdatedAt);

    /// <summary>Open review tasks (the inbox), oldest first.</summary>
    public static ListFields<ReviewTask> Inbox(IReadOnlyList<ListOption> services, IMessages messages) =>
        new ListFields<ReviewTask>(q => q.OrderBy(t => t.CreatedAt).ThenBy(t => t.ApplicationNumber))
            .Text("number", t => t.ApplicationNumber)
            .Choice("service", t => t.ServiceCode, services)
            .Text("applicant", t => t.ApplicantName)
            .Text("reference", t => t.ReferenceValue)
            .Choice("ai", t => t.AiRecommendation,
                Labelled(messages, "ai.recShort.", ["approve", "reject", "request_information", "unavailable"]))
            .Date("received", t => t.CreatedAt)
            .Date("due", t => t.DueAt);
}
