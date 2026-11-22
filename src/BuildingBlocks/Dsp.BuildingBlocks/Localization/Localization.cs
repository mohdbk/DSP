using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Dsp.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Dsp.BuildingBlocks.Web;

namespace Dsp.BuildingBlocks.Localization;

public static class LocalizationRegistration
{
    /// <summary>
    /// Registers the Localization client and the per-request language (Accept-Language → an enabled language).
    /// Inject <see cref="IMessages"/> or <see cref="RequestLanguage"/> where text is produced for the caller.
    /// </summary>
    public static IServiceCollection AddDspLocalization(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton<ILocalization>(sp => new LocalizationClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(LocalizationClient)),
            sp.GetRequiredService<ILogger<LocalizationClient>>()));
        services.AddHttpClient(nameof(LocalizationClient), c =>
        {
            c.BaseAddress = new Uri(configuration["Services:Platform"] ?? "http://platform.railway.internal:8080");
            c.DefaultRequestHeaders.Add(InternalApi.Header, configuration["Internal:ApiKey"] ?? "");
            c.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<RequestLanguage>();
        services.AddScoped<IMessages>(sp => sp.GetRequiredService<RequestLanguage>().Messages);
        return services;
    }

    /// <summary>Resolves the caller's language once per request, before the endpoints run.</summary>
    public static IApplicationBuilder UseDspLocalization(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var language = context.RequestServices.GetRequiredService<RequestLanguage>();
            await language.InitialiseAsync(context.Request.Headers.AcceptLanguage.ToString(), context.RequestAborted);
            context.Response.Headers.ContentLanguage = language.Code;
            await next(context);
        });
}

/// <summary>The language of the current request and its messages (the default language outside a request).</summary>
public sealed class RequestLanguage(ILocalization localization)
{
    private IMessages? _messages;

    public string Code => Messages.Language;
    public string DefaultLanguage => Messages.DefaultLanguage;
    public IMessages Messages => _messages ?? EmptyMessages.Instance;

    public async Task InitialiseAsync(string? acceptLanguage, CancellationToken ct) =>
        _messages = await localization.GetMessagesAsync(acceptLanguage, ct);
}

/// <summary>
/// Reads languages and catalogues from the Platform service's internal API, caching them for 30 seconds.
/// If the service can't be reached it keeps serving the last good copy (or keys, before the first one).
/// </summary>
public sealed class LocalizationClient(HttpClient http, ILogger<LocalizationClient> logger) : ILocalization
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly ConcurrentDictionary<string, (DateTimeOffset At, object Value)> _cache = new();

    public async Task<LanguageSettings> GetSettingsAsync(CancellationToken ct) =>
        await CachedAsync("settings", async () =>
                new LanguageSettings(await http.GetFromJsonAsync<List<LanguageInfo>>("/internal/platform/languages", Json, ct) ?? []),
            new LanguageSettings([new LanguageInfo("en", LocalizedText.Of("en", "English"), "ltr", true, true, 0)]));

    public async Task<IReadOnlyDictionary<string, string>> GetCatalogAsync(string language, CancellationToken ct) =>
        await CachedAsync($"catalog:{language}", async () =>
                (IReadOnlyDictionary<string, string>)(await http.GetFromJsonAsync<Dictionary<string, string>>(
                    $"/internal/platform/catalog/{Uri.EscapeDataString(language)}", Json, ct) ?? []),
            new Dictionary<string, string>());

    private async Task<T> CachedAsync<T>(string key, Func<Task<T>> load, T fallback) where T : class
    {
        if (_cache.TryGetValue(key, out var hit) && DateTimeOffset.UtcNow - hit.At < Ttl) return (T)hit.Value;
        try
        {
            var value = await load();
            _cache[key] = (DateTimeOffset.UtcNow, value);
            return value;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(ex, "Platform service unavailable; using the last known {Key}", key);
            return hit.Value as T ?? fallback;
        }
    }
}
