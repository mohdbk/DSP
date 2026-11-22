using Dsp.SharedKernel;

namespace Dsp.Payments.Domain;

public enum PaymentStatus
{
    Pending,
    Paid,
}

/// <summary>A fee owed by a payer for something another service references (e.g. an application).</summary>
public sealed class Payment : Entity
{
    private Payment() { }

    public Guid ReferenceId { get; private set; }
    public string PayerId { get; private set; } = "";
    /// <summary>What the payment is for, in every language the service has.</summary>
    public LocalizedText Description { get; private set; } = new();
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "AED";
    public PaymentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PaidAt { get; private set; }
    public string? Method { get; private set; }
    public string? TransactionReference { get; private set; }

    public static Payment Create(Guid referenceId, string payerId, decimal amount, string currency, LocalizedText description)
    {
        if (amount <= 0) throw new DomainException("errors.payment.amount");
        if (string.IsNullOrWhiteSpace(payerId)) throw new DomainException("errors.payment.payer");
        return new Payment
        {
            ReferenceId = referenceId,
            PayerId = payerId,
            Amount = amount,
            Currency = currency,
            Description = new LocalizedText(description),
            Status = PaymentStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow,
        };
    }

    public void MarkPaid(string method, string transactionReference)
    {
        if (Status == PaymentStatus.Paid) throw new DomainException("errors.payment.alreadyPaid");
        Status = PaymentStatus.Paid;
        Method = method;
        TransactionReference = transactionReference;
        PaidAt = DateTimeOffset.UtcNow;
    }
}
