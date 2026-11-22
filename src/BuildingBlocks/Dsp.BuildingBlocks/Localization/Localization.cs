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

/// <summary>A language the platform offers. Its name is itself multilingual (e.g. Arabic is "Arabic" and "العربية").</summary>
public sealed record LanguageInfo(string Code, LocalizedText Name, string Direction, bool IsEnabled, bool IsDefault, int SortOrder);

/// <summary>The platform's languages: exactly one default, any number of others, each enabled or not.</summary>
public sealed record LanguageSettings(IReadOnlyList<LanguageInfo> Languages)
{
    public string DefaultLanguage => Languages.FirstOrDefault(l => l.IsDefault)?.Code ?? "en";

    public IEnumerable<LanguageInfo> Enabled => Languages.Where(l => l.IsEnabled).OrderBy(l => l.SortOrder);

    /// <summary>Picks the best enabled language for an Accept-Language header; the default when none matches.</summary>
    public string Resolve(string? acceptLanguage)
    {
        var enabled = Enabled.Select(l => l.Code).ToList();
        foreach (var candidate in (acceptLanguage ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Select(p => p.Split(';')[0].Trim()).Where(p => p.Length > 0))
        {
            var exact = enabled.FirstOrDefault(c => string.Equals(c, candidate, StringComparison.OrdinalIgnoreCase));
            if (exact is not null) return exact;
            var neutral = enabled.FirstOrDefault(c => string.Equals(c, candidate.Split('-')[0], StringComparison.OrdinalIgnoreCase));
            if (neutral is not null) return neutral;
        }
        return DefaultLanguage;
    }
}

/// <summary>Languages and message texts, from the Platform service.</summary>
public interface ILocalization
{
    Task<LanguageSettings> GetSettingsAsync(CancellationToken ct);

    /// <summary>Every message key with its text in <paramref name="language"/>, falling back to the default language.</summary>
    Task<IReadOnlyDictionary<string, string>> GetCatalogAsync(string language, CancellationToken ct);
}

public static class LocalizationExtensions
{
    public static async Task<IMessages> GetMessagesAsync(this ILocalization localization, string? language, CancellationToken ct)
    {
        var settings = await localization.GetSettingsAsync(ct);
        var lang = language is null ? settings.DefaultLanguage : settings.Resolve(language);
        return new CatalogMessages(lang, settings.DefaultLanguage, await localization.GetCatalogAsync(lang, ct));
    }

    /// <summary>Messages in every enabled language, e.g. to store a text all readers can see in their own language.</summary>
    public static async Task<LocalizedText> RenderAllAsync(this ILocalization localization, MessageRef message, CancellationToken ct)
    {
        var settings = await localization.GetSettingsAsync(ct);
        var text = new LocalizedText();
        foreach (var language in settings.Enabled)
            text[language.Code] = message.Render(await localization.GetMessagesAsync(language.Code, ct));
        return text;
    }

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

/// <summary>Messages from a catalogue; a key without a text is shown as is.</summary>
public sealed class CatalogMessages(string language, string defaultLanguage, IReadOnlyDictionary<string, string> catalog) : IMessages
{
    public string Language { get; } = language;
    public string DefaultLanguage { get; } = defaultLanguage;

    public string Format(string key, IReadOnlyDictionary<string, string>? args = null) =>
        MessageTemplate.Fill(catalog.TryGetValue(key, out var text) ? text : key, args);
}

internal sealed class EmptyMessages : IMessages
{
    public static readonly EmptyMessages Instance = new();
    public string Language => "en";
    public string DefaultLanguage => "en";
    public string Format(string key, IReadOnlyDictionary<string, string>? args = null) => MessageTemplate.Fill(key, args);
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
