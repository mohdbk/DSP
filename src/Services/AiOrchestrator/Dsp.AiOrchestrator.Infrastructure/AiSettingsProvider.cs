using Dsp.Contracts;
using System.Net.Http.Json;
using System.Text.Json;
using Dsp.AiOrchestrator.Application;
using Microsoft.Extensions.Logging;

namespace Dsp.AiOrchestrator.Infrastructure;

/// <summary>
/// The AI settings staff manage in the back office, read from the Platform service's internal API and cached for
/// 30 seconds. If Platform can't be reached, the last good copy is used, and before the first one the environment
/// defaults (Llm__BaseUrl, Llm__ApiKey, Llm__Model).
/// </summary>
public sealed class PlatformAiSettingsProvider(HttpClient http, LlmOptions defaults, ILogger<PlatformAiSettingsProvider> logger)
    : IAiSettingsProvider
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static (DateTimeOffset At, AiSettings Value)? _cache;

    public async Task<AiSettings> GetAsync(CancellationToken ct)
    {
        if (_cache is { } hit && DateTimeOffset.UtcNow - hit.At < Ttl) return hit.Value;
        await Gate.WaitAsync(ct);
        try
        {
            if (_cache is { } again && DateTimeOffset.UtcNow - again.At < Ttl) return again.Value;
            var value = await http.GetFromJsonAsync<AiSettings>("/internal/platform/ai", Json, ct)
                        ?? throw new JsonException("Empty AI settings.");
            _cache = (DateTimeOffset.UtcNow, value);
            return value;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Platform AI settings unavailable; using the last known settings or the environment defaults");
            return _cache?.Value ?? FromEnvironment();
        }
        finally
        {
            Gate.Release();
        }
    }

    private AiSettings FromEnvironment() => new("OpenAI-compatible", defaults.BaseUrl ?? "", defaults.Model ?? "",
        defaults.ApiKey, defaults.Temperature, 90, false, 6, 30, null, null, AiPrompts.DefaultChat, AiPrompts.DefaultReview);
}
