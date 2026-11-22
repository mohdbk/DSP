using System.Net.Http.Json;
using Dsp.EServices.Application;

namespace Dsp.EServices.Infrastructure.Clients;

/// <summary>Calls the Payments service's internal API (not exposed through the gateway).</summary>
public sealed class PaymentsGateway(HttpClient http) : IPaymentsGateway
{
    public async Task<Guid> CreatePaymentAsync(Guid applicationId, string payerId, decimal amount, string currency,
        string description, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync("/internal/payments", new
        {
            referenceId = applicationId,
            payerId,
            amount,
            currency,
            description,
        }, ct);
        response.EnsureSuccessStatusCode();
        var created = await response.Content.ReadFromJsonAsync<Created>(ct)
                      ?? throw new InvalidOperationException("Payments returned no body.");
        return created.Id;
    }

    private sealed record Created(Guid Id);
}
