using Dsp.SharedKernel;
using System.Text.Json;
using Dsp.BuildingBlocks.Messaging;
using Dsp.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dsp.UnitTests.Messaging;

public class OutboxTests
{
    private sealed class TestDb(DbContextOptions<TestDb> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.AddMessagingTables();
    }

    private static ServiceProvider Services(IMessagePublisher publisher)
    {
        var name = Guid.NewGuid().ToString();
        return new ServiceCollection()
            .AddLogging()
            .AddDbContext<TestDb>(o => o.UseInMemoryDatabase(name))
            .AddSingleton(publisher)
            .AddSingleton<IHostedService, OutboxDispatcher<TestDb>>()
            .BuildServiceProvider();
    }

    private static ApplicationStatusChanged SampleEvent() =>
        new(Guid.NewGuid(), "DSP-2026-000001", "SVC", "Service", "uaepass:a", "Submitted", MessageRef.Of("status.notify.submitted"));

    [Fact]
    public async Task Event_is_staged_with_the_unit_of_work_and_not_before_commit()
    {
        await using var sp = Services(new RecordingPublisher());
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDb>();
        var evt = SampleEvent();

        await new OutboxEventBus<TestDb>(db).PublishAsync(evt);

        await using (var other = sp.CreateAsyncScope())
            Assert.Empty(other.ServiceProvider.GetRequiredService<TestDb>().Set<OutboxMessage>());

        await db.SaveChangesAsync();

        var stored = Assert.Single(db.Set<OutboxMessage>());
        Assert.Equal(evt.EventId, stored.Id);
        Assert.Equal(nameof(ApplicationStatusChanged), stored.Type);
        Assert.Equal("DSP-2026-000001",
            JsonDocument.Parse(stored.Payload).RootElement.GetProperty("applicationNumber").GetString());
        Assert.Null(stored.PublishedAt);
    }

    [Fact]
    public async Task Dispatcher_publishes_committed_messages_in_order_and_marks_them()
    {
        var publisher = new RecordingPublisher();
        await using var sp = Services(publisher);
        var first = SampleEvent() with { OccurredAt = DateTimeOffset.UtcNow.AddSeconds(-5) };
        var second = SampleEvent();
        await Seed(sp, second, first);

        await RunDispatcherUntil(sp, () => publisher.Sent.Count == 2);

        Assert.Equal(new[] { first.EventId, second.EventId }, publisher.Sent.Select(s => s.Id));
        await using var scope = sp.CreateAsyncScope();
        Assert.All(scope.ServiceProvider.GetRequiredService<TestDb>().Set<OutboxMessage>(), m => Assert.NotNull(m.PublishedAt));
    }

    [Fact]
    public async Task Broker_failure_keeps_the_message_for_retry()
    {
        await using var sp = Services(new FailingPublisher());
        var evt = SampleEvent();
        await Seed(sp, evt);

        await RunDispatcherUntil(sp, () =>
        {
            using var scope = sp.CreateScope();
            return scope.ServiceProvider.GetRequiredService<TestDb>().Set<OutboxMessage>().Single().Attempts > 0;
        });

        await using var check = sp.CreateAsyncScope();
        var message = check.ServiceProvider.GetRequiredService<TestDb>().Set<OutboxMessage>().Single();
        Assert.Null(message.PublishedAt);
        Assert.Equal("broker unavailable", message.LastError);
    }

    [Fact]
    public async Task Inbox_remembers_handled_events()
    {
        await using var sp = Services(new RecordingPublisher());
        var id = Guid.NewGuid();

        await using (var scope = sp.CreateAsyncScope())
        {
            var inbox = new EfInboxStore<TestDb>(scope.ServiceProvider.GetRequiredService<TestDb>());
            Assert.False(await inbox.HasProcessedAsync(id, default));
            inbox.MarkProcessed(id, "PaymentCompleted");
            await inbox.SaveChangesAsync(default);
        }

        await using (var scope = sp.CreateAsyncScope())
        {
            var inbox = new EfInboxStore<TestDb>(scope.ServiceProvider.GetRequiredService<TestDb>());
            Assert.True(await inbox.HasProcessedAsync(id, default));
        }
    }

    private static async Task Seed(IServiceProvider sp, params IIntegrationEvent[] events)
    {
        await using var scope = sp.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TestDb>();
        var bus = new OutboxEventBus<TestDb>(db);
        foreach (var e in events) await bus.PublishAsync((ApplicationStatusChanged)e);
        await db.SaveChangesAsync();
    }

    private static async Task RunDispatcherUntil(IServiceProvider sp, Func<bool> done)
    {
        var dispatcher = sp.GetRequiredService<IHostedService>();
        await dispatcher.StartAsync(default);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!done() && DateTime.UtcNow < deadline) await Task.Delay(50);
            Assert.True(done(), "Dispatcher did not reach the expected state in time.");
        }
        finally
        {
            await dispatcher.StopAsync(default);
        }
    }

    private sealed class RecordingPublisher : IMessagePublisher
    {
        public List<(string Type, Guid Id)> Sent { get; } = [];

        public Task PublishAsync(string type, Guid messageId, string payload, CancellationToken ct)
        {
            lock (Sent) Sent.Add((type, messageId));
            return Task.CompletedTask;
        }
    }

    private sealed class FailingPublisher : IMessagePublisher
    {
        public Task PublishAsync(string type, Guid messageId, string payload, CancellationToken ct) =>
            throw new InvalidOperationException("broker unavailable");
    }
}
