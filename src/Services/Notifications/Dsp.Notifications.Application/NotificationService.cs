using Dsp.Contracts;
using Dsp.Notifications.Domain;
using Dsp.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Dsp.Notifications.Application;

public interface INotificationRepository
{
    Task<Notification?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Notification>> ListAsync(IReadOnlyCollection<string> recipients, int take, CancellationToken ct);
    Task AddAsync(Notification notification, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>A notification in the reader's language.</summary>
public sealed record NotificationDto(Guid Id, string Title, string Body, string? Link, DateTimeOffset CreatedAt, bool IsRead)
{
    public static NotificationDto From(Notification n, IMessages messages) =>
        new(n.Id, n.RenderTitle(messages), n.RenderBody(messages), n.Link, n.CreatedAt, n.ReadAt.HasValue);
}

public sealed class NotificationService(INotificationRepository notifications, IMessages messages)
{
    public async Task<IReadOnlyList<NotificationDto>> ListAsync(string userId, bool isStaff, CancellationToken ct)
    {
        string[] recipients = isStaff ? [userId, Notification.StaffAudience] : [userId];
        return (await notifications.ListAsync(recipients, 50, ct)).Select(n => NotificationDto.From(n, messages)).ToList();
    }

    public async Task MarkReadAsync(string userId, bool isStaff, Guid id, CancellationToken ct)
    {
        var notification = await notifications.GetAsync(id, ct) ?? throw new NotFoundException("errors.notification.notFound");
        var mine = notification.Recipient == userId || (isStaff && notification.Recipient == Notification.StaffAudience);
        if (!mine) throw new NotFoundException("errors.notification.notFound");
        notification.MarkRead();
        await notifications.SaveChangesAsync(ct);
    }
}

/// <summary>Turns application status changes into notifications for the applicant (and staff, for new work).</summary>
public sealed class ApplicationStatusChangedHandler(INotificationRepository notifications)
    : IIntegrationEventHandler<ApplicationStatusChanged>
{
    public async Task HandleAsync(ApplicationStatusChanged e, CancellationToken ct)
    {
        var title = MessageRef.Of("notifications.applicationTitle", ("service", e.ServiceName), ("number", e.ApplicationNumber));
        await notifications.AddAsync(Notification.Create(e.ApplicantId, title, e.Message, $"/applications/{e.ApplicationId}"), ct);

        if (e.Status == "UnderReview")
        {
            await notifications.AddAsync(Notification.Create(Notification.StaffAudience,
                MessageRef.Of("notifications.staffNewTitle", ("number", e.ApplicationNumber)),
                MessageRef.Of("notifications.staffNewBody", ("service", e.ServiceName)),
                "/inbox"), ct);
        }

        await notifications.SaveChangesAsync(ct);
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services) =>
        services.AddScoped<NotificationService>();
}
