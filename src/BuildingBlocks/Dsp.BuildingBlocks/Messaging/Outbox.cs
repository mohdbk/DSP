using System.Text.Json;
using Dsp.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dsp.BuildingBlocks.Messaging;

/// <summary>
/// An integration event waiting to be published. Written in the same transaction as the business
/// change that raised it, so an event is never lost and never published for a change that rolled back.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
}

/// <summary>An integration event this service has already handled (makes consumers idempotent).</summary>
public sealed class InboxMessage
{
    public Guid Id { get; set; }
    public string Type { get; set; } = "";
    public DateTimeOffset ProcessedAt { get; set; }
}

public static class MessagingModel
{
    /// <summary>Adds the outbox and inbox tables to a service's model.</summary>
    public static ModelBuilder AddMessagingTables(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.ToTable("OutboxMessages");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PublishedAt, x.OccurredAt });
            e.Property(x => x.Type).HasMaxLength(200);
            e.Property(x => x.LastError).HasMaxLength(2000);
        });
        modelBuilder.Entity<InboxMessage>(e =>
        {
            e.ToTable("InboxMessages");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(200);
        });
        return modelBuilder;
    }
}

/// <summary>
/// IEventBus that stages events in the outbox of the current unit of work.
/// Callers publish before SaveChanges; the dispatcher sends them after commit.
/// </summary>
public sealed class OutboxEventBus<TContext>(TContext db) : IEventBus where TContext : DbContext
{
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent
    {
        db.Set<OutboxMessage>().Add(new OutboxMessage
        {
            Id = @event.EventId,
            Type = typeof(TEvent).Name,
            Payload = JsonSerializer.Serialize(@event, MessagingJson.Options),
            OccurredAt = @event.OccurredAt,
        });
        return Task.CompletedTask;
    }
}

/// <summary>Publishes committed outbox messages in order, retrying until the broker confirms them.</summary>
public sealed class OutboxDispatcher<TContext>(
    IServiceScopeFactory scopeFactory,
    IMessagePublisher publisher,
    ILogger<OutboxDispatcher<TContext>> logger) : BackgroundService where TContext : DbContext
{
    private const int BatchSize = 50;
    private static readonly TimeSpan Idle = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var published = 0;
            try
            {
                published = await DispatchBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Outbox dispatch failed: {Error}", ex.GetBaseException().Message);
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }

            if (published < BatchSize)
            {
                try { await Task.Delay(Idle, stoppingToken); }
                catch (OperationCanceledException) { }
            }
        }
    }

    private async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var outbox = db.Set<OutboxMessage>();

        // Single dispatcher per service in the POC. With several replicas, add row locking or a leader lease.
        var batch = await outbox.Where(m => m.PublishedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var message in batch)
        {
            try
            {
                await publisher.PublishAsync(message.Type, message.Id, message.Payload, ct);
                message.PublishedAt = DateTimeOffset.UtcNow;
                message.LastError = null;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.LastError = ex.GetBaseException().Message;
                logger.LogWarning("Could not publish {Type} {Id} (attempt {Attempt}): {Error}", message.Type, message.Id,
                    message.Attempts, message.LastError);
                await db.SaveChangesAsync(ct);
                break; // keep order: stop at the first failure and retry later
            }
        }

        if (batch.Count > 0) await db.SaveChangesAsync(ct);
        return batch.Count;
    }
}

/// <summary>Records handled events so redelivered messages are skipped.</summary>
public interface IInboxStore
{
    Task<bool> HasProcessedAsync(Guid eventId, CancellationToken ct);
    void MarkProcessed(Guid eventId, string type);
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>
/// Inbox in the service's own DbContext. The marker is added to the same unit of work as the handler's
/// changes, so "handled" and its effects commit together.
/// </summary>
public sealed class EfInboxStore<TContext>(TContext db) : IInboxStore where TContext : DbContext
{
    public Task<bool> HasProcessedAsync(Guid eventId, CancellationToken ct) =>
        db.Set<InboxMessage>().AnyAsync(m => m.Id == eventId, ct);

    public void MarkProcessed(Guid eventId, string type) =>
        db.Set<InboxMessage>().Add(new InboxMessage { Id = eventId, Type = type, ProcessedAt = DateTimeOffset.UtcNow });

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.ChangeTracker.HasChanges() ? db.SaveChangesAsync(ct) : Task.CompletedTask;
}
