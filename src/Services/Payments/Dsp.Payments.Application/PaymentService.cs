using Dsp.Contracts;
using Dsp.Payments.Domain;
using Dsp.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Dsp.Payments.Application;

public interface IPaymentRepository
{
    Task<Payment?> GetAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<Payment>> ListAsync(string payerId, Guid? referenceId, CancellationToken ct);
    Task AddAsync(Payment payment, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Charges the payer. The POC gateway approves instantly; a real gateway plugs in here.</summary>
public interface IPaymentProcessor
{
    string Method { get; }
    Task<string> ChargeAsync(Payment payment, CancellationToken ct);
}

public sealed record CreatePaymentRequest(Guid ReferenceId, string PayerId, decimal Amount, string Currency,
    string Description);

public sealed record PaymentDto(Guid Id, Guid ReferenceId, string Description, decimal Amount, string Currency,
    string Status, DateTimeOffset CreatedAt, DateTimeOffset? PaidAt, string? Method, string? TransactionReference)
{
    public static PaymentDto From(Payment p) => new(p.Id, p.ReferenceId, p.Description, p.Amount, p.Currency,
        p.Status.ToString(), p.CreatedAt, p.PaidAt, p.Method, p.TransactionReference);
}

public sealed class PaymentService(IPaymentRepository payments, IPaymentProcessor processor, IEventBus bus)
{
    public async Task<PaymentDto> CreateAsync(CreatePaymentRequest req, CancellationToken ct)
    {
        var payment = Payment.Create(req.ReferenceId, req.PayerId, req.Amount, req.Currency, req.Description);
        await payments.AddAsync(payment, ct);
        await payments.SaveChangesAsync(ct);
        return PaymentDto.From(payment);
    }

    public async Task<PaymentDto> GetAsync(string payerId, bool isStaff, Guid id, CancellationToken ct) =>
        PaymentDto.From(await LoadVisibleAsync(payerId, isStaff, id, ct));

    public async Task<IReadOnlyList<PaymentDto>> ListAsync(string payerId, Guid? referenceId, CancellationToken ct) =>
        (await payments.ListAsync(payerId, referenceId, ct)).Select(PaymentDto.From).ToList();

    public async Task<PaymentDto> PayAsync(string payerId, Guid id, CancellationToken ct)
    {
        var payment = await LoadVisibleAsync(payerId, isStaff: false, id, ct);
        var transaction = await processor.ChargeAsync(payment, ct);
        payment.MarkPaid(processor.Method, transaction);
        await payments.SaveChangesAsync(ct);

        await bus.PublishAsync(new PaymentCompleted(payment.Id, payment.ReferenceId, payment.PayerId, payment.Amount,
            payment.Currency), ct);
        return PaymentDto.From(payment);
    }

    private async Task<Payment> LoadVisibleAsync(string payerId, bool isStaff, Guid id, CancellationToken ct)
    {
        var payment = await payments.GetAsync(id, ct) ?? throw new NotFoundException("Payment not found.");
        if (!isStaff && payment.PayerId != payerId) throw new NotFoundException("Payment not found.");
        return payment;
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddPaymentsApplication(this IServiceCollection services) =>
        services.AddScoped<PaymentService>();
}
