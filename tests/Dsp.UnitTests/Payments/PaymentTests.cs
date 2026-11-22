using Dsp.Contracts;
using Dsp.Payments.Application;
using Dsp.Payments.Domain;
using Dsp.SharedKernel;
using Dsp.UnitTests.EServices;

namespace Dsp.UnitTests.Payments;

public class PaymentTests
{
    [Fact]
    public void Amount_must_be_positive()
    {
        Assert.Throws<DomainException>(() => Payment.Create(Guid.NewGuid(), "uaepass:a", 0, "AED", "Fee"));
    }

    [Fact]
    public void Payment_can_only_be_paid_once()
    {
        var payment = Payment.Create(Guid.NewGuid(), "uaepass:a", 200, "AED", "Fee");
        payment.MarkPaid("Simulated card", "SIM-1");

        Assert.Equal(PaymentStatus.Paid, payment.Status);
        Assert.Throws<DomainException>(() => payment.MarkPaid("Simulated card", "SIM-2"));
    }
}

public class PaymentServiceTests
{
    private readonly List<string> _log = [];
    private readonly Store _store;
    private readonly RecordingBus _bus;
    private readonly PaymentService _service;

    public PaymentServiceTests()
    {
        _store = new Store(_log);
        _bus = new RecordingBus(_log);
        _service = new PaymentService(_store, new Processor(), _bus);
    }

    [Fact]
    public async Task Paying_stages_PaymentCompleted_before_committing()
    {
        var applicationId = Guid.NewGuid();
        var created = await _service.CreateAsync(new CreatePaymentRequest(applicationId, "uaepass:a", 200, "AED", "Fee"), default);
        _log.Clear();

        var paid = await _service.PayAsync("uaepass:a", created.Id, default);

        Assert.Equal("Paid", paid.Status);
        var completed = Assert.IsType<PaymentCompleted>(Assert.Single(_bus.Events));
        Assert.Equal(applicationId, completed.ReferenceId);
        Assert.Equal(200, completed.Amount);
        Assert.Equal(new[] { "publish:PaymentCompleted", "save" }, _log);
    }

    [Fact]
    public async Task Someone_else_cannot_pay_or_see_the_payment()
    {
        var created = await _service.CreateAsync(new CreatePaymentRequest(Guid.NewGuid(), "uaepass:a", 200, "AED", "Fee"), default);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.PayAsync("uaepass:b", created.Id, default));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync("uaepass:b", false, created.Id, default));
        Assert.Equal(created.Id, (await _service.GetAsync("staff:x", true, created.Id, default)).Id);
    }

    private sealed class Store(List<string> log) : IPaymentRepository
    {
        private readonly List<Payment> _items = [];
        public Task<Payment?> GetAsync(Guid id, CancellationToken ct) => Task.FromResult(_items.FirstOrDefault(p => p.Id == id));
        public Task<IReadOnlyList<Payment>> ListAsync(string payerId, Guid? referenceId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Payment>>(_items.Where(p => p.PayerId == payerId).ToList());
        public Task AddAsync(Payment payment, CancellationToken ct) { _items.Add(payment); return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct) { log.Add("save"); return Task.CompletedTask; }
    }

    private sealed class Processor : IPaymentProcessor
    {
        public string Method => "Test";
        public Task<string> ChargeAsync(Payment payment, CancellationToken ct) => Task.FromResult("TX-1");
    }
}
