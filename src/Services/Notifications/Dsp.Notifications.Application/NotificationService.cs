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

public sealed record NotificationDto(Guid Id, string Title, string Body, string? TitleAr, string? BodyAr, string? Link,
    DateTimeOffset CreatedAt, bool IsRead)
{
    public static NotificationDto From(Notification n) =>
        new(n.Id, n.Title, n.Body, n.TitleAr, n.BodyAr, n.Link, n.CreatedAt, n.ReadAt.HasValue);
}

public sealed class NotificationService(INotificationRepository notifications)
{
    public async Task<IReadOnlyList<NotificationDto>> ListAsync(string userId, bool isStaff, CancellationToken ct)
    {
        string[] recipients = isStaff ? [userId, Notification.StaffAudience] : [userId];
        return (await notifications.ListAsync(recipients, 50, ct)).Select(NotificationDto.From).ToList();
    }

    public async Task MarkReadAsync(string userId, bool isStaff, Guid id, CancellationToken ct)
    {
        var notification = await notifications.GetAsync(id, ct) ?? throw new NotFoundException("Notification not found.");
        var mine = notification.Recipient == userId || (isStaff && notification.Recipient == Notification.StaffAudience);
        if (!mine) throw new NotFoundException("Notification not found.");
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
        await notifications.AddAsync(Notification.Create(e.ApplicantId, $"{e.ServiceName} · {e.ApplicationNumber}",
            e.Message, $"/applications/{e.ApplicationId}", $"{e.ServiceName} · {e.ApplicationNumber}", e.MessageAr), ct);

        if (e.Status == "UnderReview")
        {
            await notifications.AddAsync(Notification.Create(Notification.StaffAudience,
                $"New application to review · {e.ApplicationNumber}", $"{e.ServiceName} is waiting for a decision.",
                "/inbox", $"طلب جديد للمراجعة · {e.ApplicationNumber}", $"{e.ServiceName} بانتظار القرار."), ct);
        }

        await notifications.SaveChangesAsync(ct);
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsApplication(this IServiceCollection services) =>
        services.AddScoped<NotificationService>();
}
