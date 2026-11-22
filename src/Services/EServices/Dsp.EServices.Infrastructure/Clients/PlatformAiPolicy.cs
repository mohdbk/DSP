using System.Net.Http.Json;
using System.Text.Json;
using Dsp.EServices.Application;
using Microsoft.Extensions.Logging;

namespace Dsp.EServices.Infrastructure.Clients;

/// <summary>
/// Reads the AI policy from the Platform service (only the policy: never the model's address or key), cached for
/// 30 seconds. When Platform can't be reached it answers 100%, so every case goes to an officer.
/// </summary>
public sealed class PlatformAiPolicy(HttpClient http, ILogger<PlatformAiPolicy> logger) : IAiPolicy
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static (DateTimeOffset At, int Value)? _cache;

    private sealed record Policy(int AuditSamplePercent);

    public async Task<int> GetAuditSamplePercentAsync(CancellationToken ct)
    {
        if (_cache is { } hit && DateTimeOffset.UtcNow - hit.At < Ttl) return hit.Value;
        try
        {
            var policy = await http.GetFromJsonAsync<Policy>("/internal/platform/ai/policy", Json, ct)
                         ?? throw new JsonException("Empty AI policy.");
            var value = Math.Clamp(policy.AuditSamplePercent, 0, 100);
            _cache = (DateTimeOffset.UtcNow, value);
            return value;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "AI policy unavailable; sending every agentic case to an officer");
            return _cache?.Value ?? 100;
        }
    }
}
