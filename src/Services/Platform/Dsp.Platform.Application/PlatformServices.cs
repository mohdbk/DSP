using Dsp.Contracts;
using Dsp.Platform.Domain;
using Dsp.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace Dsp.Platform.Application;

// ---- Ports ----

public interface ILanguageRepository
{
    Task<IReadOnlyList<Language>> ListAsync(CancellationToken ct);
    Task<Language?> GetAsync(string code, CancellationToken ct);
    Task AddAsync(Language language, CancellationToken ct);
    void Remove(Language language);
    Task SaveChangesAsync(CancellationToken ct);
}

public interface ITranslationRepository
{
    Task<IReadOnlyList<Translation>> ListAsync(string? languageCode, CancellationToken ct);
    Task<Translation?> GetAsync(string key, string languageCode, CancellationToken ct);
    Task AddAsync(Translation translation, CancellationToken ct);
    void Remove(Translation translation);
    Task RemoveLanguageAsync(string languageCode, CancellationToken ct);
    Task RemoveKeyAsync(string key, CancellationToken ct);
}

public interface IAiSettingsRepository
{
    Task<AiSettings> GetAsync(CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}

/// <summary>Encrypts secrets (the AI API key) at rest.</summary>
public interface ISecretProtector
{
    string Protect(string secret);
    string? Unprotect(string protectedSecret);
}

/// <summary>Checks that the model endpoint answers with the given settings.</summary>
public interface IAiConnectionTester
{
    Task<AiTestResultDto> TestAsync(AiRuntimeSettingsDto settings, CancellationToken ct);
}

/// <summary>Defaults from configuration, used until staff save AI settings in the back office.</summary>
public sealed record AiDefaults(string? BaseUrl, string? ApiKey, string? Model);

// ---- DTOs ----

public sealed record LanguageDto(string Code, LocalizedText Name, string Direction, bool IsEnabled, bool IsDefault, int SortOrder)
{
    public static LanguageDto From(Language l) => new(l.Code, l.Name, l.Direction, l.IsEnabled, l.IsDefault, l.SortOrder);
}

public sealed record LanguageAdminDto(string Code, LocalizedText Name, string Direction, bool IsEnabled, bool IsDefault,
    int SortOrder, int Translated, int Total, DateTimeOffset UpdatedAt, string UpdatedBy);

public sealed record LanguageRequest(string Code, LocalizedText Name, string Direction, int SortOrder);

public sealed record CatalogDto(string Language, string DefaultLanguage, string Direction, IReadOnlyDictionary<string, string> Strings);

public sealed record TranslationRowDto(string Key, string DefaultText, string? Text, DateTimeOffset? UpdatedAt, string? UpdatedBy);

public sealed record SetTranslationRequest(string? Text);

public sealed record ImportResultDto(int Added, int Updated, int Skipped);

public sealed record AiSettingsDto(string ProviderName, string BaseUrl, string Model, bool HasApiKey, double Temperature,
    int TimeoutSeconds, bool ChatEnabled, bool ReviewEnabled, int MaxToolIterations, int MaxHistoryMessages,
    string? ChatInstructions, string? ReviewInstructions, string? ChatPrompt, string? ReviewPrompt,
    string DefaultChatPrompt, string DefaultReviewPrompt, bool IsConfigured, DateTimeOffset UpdatedAt, string UpdatedBy);

/// <summary>
/// Saving AI settings. <c>ApiKey</c>: null keeps the current key, "" removes it, anything else replaces it.
/// <c>ChatPrompt</c>/<c>ReviewPrompt</c>: null, empty or the shipped default means "use the default".
/// </summary>
public sealed record AiSettingsRequest(string? ProviderName, string? BaseUrl, string? Model, string? ApiKey, double Temperature,
    int TimeoutSeconds, bool ChatEnabled, bool ReviewEnabled, int MaxToolIterations, int MaxHistoryMessages,
    string? ChatInstructions, string? ReviewInstructions, string? ChatPrompt = null, string? ReviewPrompt = null);

/// <summary>
/// Everything the AI Orchestrator needs to call the model, with the key decrypted (internal API only).
/// The prompts are the ones in force (staff's version or the default), placeholders not yet filled in.
/// </summary>
public sealed record AiRuntimeSettingsDto(string ProviderName, string BaseUrl, string Model, string? ApiKey, double Temperature,
    int TimeoutSeconds, bool ChatEnabled, bool ReviewEnabled, int MaxToolIterations, int MaxHistoryMessages,
    string? ChatInstructions, string? ReviewInstructions, string ChatPrompt, string ReviewPrompt)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Model);
}

/// <summary>Result of a connection test: <c>MessageKey</c> is a message key (shown in the reader's language).</summary>
public sealed record AiTestResultDto(bool Ok, string MessageKey, string? Detail, int? LatencyMs);

/// <summary>The signed-in staff member making a change.</summary>
public sealed record StaffCaller(string UserId, string Name, bool IsStaff);

// ---- Use cases ----

/// <summary>Languages: listing for everyone, and managing them for staff.</summary>
public sealed class LanguageService(ILanguageRepository languages, ITranslationRepository translations)
{
    public async Task<IReadOnlyList<LanguageDto>> ListEnabledAsync(CancellationToken ct) =>
        (await languages.ListAsync(ct)).Where(l => l.IsEnabled).OrderBy(l => l.SortOrder).ThenBy(l => l.Code)
            .Select(LanguageDto.From).ToList();

    public async Task<IReadOnlyList<LanguageDto>> ListAllAsync(CancellationToken ct) =>
        (await languages.ListAsync(ct)).OrderBy(l => l.SortOrder).ThenBy(l => l.Code).Select(LanguageDto.From).ToList();

    public async Task<IReadOnlyList<LanguageAdminDto>> ListForAdminAsync(StaffCaller caller, CancellationToken ct)
    {
        EnsureStaff(caller);
        var all = await languages.ListAsync(ct);
        var allTranslations = await translations.ListAsync(null, ct);
        var defaultCode = all.FirstOrDefault(l => l.IsDefault)?.Code;
        var keys = allTranslations.Where(t => t.LanguageCode == defaultCode).Select(t => t.Key).ToHashSet();
        return all.OrderBy(l => l.SortOrder).ThenBy(l => l.Code).Select(l => new LanguageAdminDto(
                l.Code, l.Name, l.Direction, l.IsEnabled, l.IsDefault, l.SortOrder,
                allTranslations.Count(t => t.LanguageCode == l.Code && keys.Contains(t.Key)), keys.Count, l.UpdatedAt, l.UpdatedBy))
            .ToList();
    }

    public async Task<LanguageDto> CreateAsync(StaffCaller caller, LanguageRequest request, CancellationToken ct)
    {
        EnsureStaff(caller);
        if (await languages.GetAsync(request.Code.Trim(), ct) is not null)
            throw new ValidationException([new FieldError("code", "errors.language.exists")]);
        var language = Language.Create(request.Code, request.Name, request.Direction, request.SortOrder, caller.Name);
        await languages.AddAsync(language, ct);
        await languages.SaveChangesAsync(ct);
        return LanguageDto.From(language);
    }

    public async Task<LanguageDto> UpdateAsync(StaffCaller caller, string code, LanguageRequest request, CancellationToken ct)
    {
        EnsureStaff(caller);
        var language = await GetAsync(code, ct);
        language.Update(request.Name, request.Direction, request.SortOrder, caller.Name);
        await languages.SaveChangesAsync(ct);
        return LanguageDto.From(language);
    }

    public async Task<LanguageDto> SetEnabledAsync(StaffCaller caller, string code, bool enabled, CancellationToken ct)
    {
        EnsureStaff(caller);
        var language = await GetAsync(code, ct);
        if (enabled) language.Enable(caller.Name); else language.Disable(caller.Name);
        await languages.SaveChangesAsync(ct);
        return LanguageDto.From(language);
    }

    public async Task<LanguageDto> SetDefaultAsync(StaffCaller caller, string code, CancellationToken ct)
    {
        EnsureStaff(caller);
        var all = await languages.ListAsync(ct);
        var language = all.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase))
                       ?? throw new NotFoundException("errors.language.notFound");
        LanguagePolicy.SetDefault(all, language, caller.Name);
        await languages.SaveChangesAsync(ct);
        return LanguageDto.From(language);
    }

    /// <summary>Deletes a language and all its translations. The default language can't be deleted.</summary>
    public async Task DeleteAsync(StaffCaller caller, string code, CancellationToken ct)
    {
        EnsureStaff(caller);
        var language = await GetAsync(code, ct);
        LanguagePolicy.EnsureCanDelete(language);
        await translations.RemoveLanguageAsync(language.Code, ct);
        languages.Remove(language);
        await languages.SaveChangesAsync(ct);
    }

    private async Task<Language> GetAsync(string code, CancellationToken ct) =>
        await languages.GetAsync(code, ct) ?? throw new NotFoundException("errors.language.notFound");

    internal static void EnsureStaff(StaffCaller caller)
    {
        if (!caller.IsStaff) throw new ForbiddenException("errors.forbidden");
    }
}

/// <summary>Message texts: catalogues for the portals and services, and translation management for staff.</summary>
public sealed class TranslationService(ILanguageRepository languages, ITranslationRepository translations)
{
    /// <summary>Every key in <paramref name="code"/>, with the default language's text where it has none.</summary>
    public async Task<CatalogDto> GetCatalogAsync(string code, CancellationToken ct)
    {
        var all = await languages.ListAsync(ct);
        var defaultLanguage = all.FirstOrDefault(l => l.IsDefault)?.Code ?? "en";
        var language = all.FirstOrDefault(l => l.IsEnabled && string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase))
                       ?? all.First(l => l.Code == defaultLanguage);

        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var t in await translations.ListAsync(defaultLanguage, ct)) strings[t.Key] = t.Text;
        if (language.Code != defaultLanguage)
            foreach (var t in await translations.ListAsync(language.Code, ct))
                if (strings.ContainsKey(t.Key)) strings[t.Key] = t.Text;
        return new CatalogDto(language.Code, defaultLanguage, language.Direction, strings);
    }

    /// <summary>One page of keys with the default text and the text in <paramref name="code"/>.</summary>
    public async Task<PagedList<TranslationRowDto>> ListAsync(StaffCaller caller, string code, string? search, bool missingOnly,
        int? page, int? pageSize, CancellationToken ct)
    {
        LanguageService.EnsureStaff(caller);
        var defaultCode = await DefaultCodeAsync(ct);
        var defaults = await translations.ListAsync(defaultCode, ct);
        var target = (await translations.ListAsync(code, ct)).ToDictionary(t => t.Key);

        var rows = defaults.OrderBy(t => t.Key, StringComparer.Ordinal)
            .Select(d => target.TryGetValue(d.Key, out var t)
                ? new TranslationRowDto(d.Key, d.Text, t.Text, t.UpdatedAt, t.UpdatedBy)
                : new TranslationRowDto(d.Key, d.Text, null, null, null))
            .Where(r => !missingOnly || r.Text is null)
            .Where(r => string.IsNullOrWhiteSpace(search) ||
                        r.Key.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        r.DefaultText.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                        (r.Text?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();
        return PagedList.From(rows, page, pageSize);
    }

    /// <summary>Sets (or with an empty text removes) one key's text in one language. New keys start in the default language.</summary>
    public async Task SetAsync(StaffCaller caller, string code, string key, string? text, CancellationToken ct)
    {
        LanguageService.EnsureStaff(caller);
        var language = await languages.GetAsync(code, ct) ?? throw new NotFoundException("errors.language.notFound");
        var existing = await translations.GetAsync(key, language.Code, ct);

        if (string.IsNullOrWhiteSpace(text))
        {
            if (language.IsDefault) throw new ValidationException([new FieldError("text", "errors.translation.defaultRequired")]);
            if (existing is not null) translations.Remove(existing);
        }
        else if (existing is not null) existing.Change(text, caller.Name);
        else
        {
            if (!language.IsDefault && await translations.GetAsync(key, await DefaultCodeAsync(ct), ct) is null)
                throw new ValidationException([new FieldError("key", "errors.translation.unknownKey")]);
            await translations.AddAsync(Translation.Create(key, language.Code, text, caller.Name), ct);
        }
        await languages.SaveChangesAsync(ct);
    }

    /// <summary>Removes a key in every language.</summary>
    public async Task DeleteKeyAsync(StaffCaller caller, string key, CancellationToken ct)
    {
        LanguageService.EnsureStaff(caller);
        await translations.RemoveKeyAsync(key, ct);
        await languages.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyDictionary<string, string>> ExportAsync(StaffCaller caller, string code, CancellationToken ct)
    {
        LanguageService.EnsureStaff(caller);
        return (await translations.ListAsync(code, ct)).OrderBy(t => t.Key, StringComparer.Ordinal)
            .ToDictionary(t => t.Key, t => t.Text);
    }

    /// <summary>Imports key → text pairs for a language (e.g. from a translator). Unknown keys are skipped unless it is the default language.</summary>
    public async Task<ImportResultDto> ImportAsync(StaffCaller caller, string code, IReadOnlyDictionary<string, string> strings,
        CancellationToken ct)
    {
        LanguageService.EnsureStaff(caller);
        var language = await languages.GetAsync(code, ct) ?? throw new NotFoundException("errors.language.notFound");
        var keys = (await translations.ListAsync(await DefaultCodeAsync(ct), ct)).Select(t => t.Key).ToHashSet();
        var existing = (await translations.ListAsync(language.Code, ct)).ToDictionary(t => t.Key);
        int added = 0, updated = 0, skipped = 0;

        foreach (var (key, text) in strings)
        {
            if (string.IsNullOrWhiteSpace(text) || (!language.IsDefault && !keys.Contains(key))) { skipped++; continue; }
            if (existing.TryGetValue(key, out var t))
            {
                if (t.Text != text) { t.Change(text, caller.Name); updated++; } else skipped++;
            }
            else
            {
                await translations.AddAsync(Translation.Create(key, language.Code, text, caller.Name), ct);
                added++;
            }
        }
        await languages.SaveChangesAsync(ct);
        return new ImportResultDto(added, updated, skipped);
    }

    private async Task<string> DefaultCodeAsync(CancellationToken ct) =>
        (await languages.ListAsync(ct)).FirstOrDefault(l => l.IsDefault)?.Code ?? "en";
}

/// <summary>AI settings: staff edit them (the key is write-only); the AI Orchestrator reads them internally.</summary>
public sealed class AiSettingsService(IAiSettingsRepository settings, ISecretProtector protector, AiDefaults defaults,
    IAiConnectionTester tester)
{
    /// <summary>Calls the model with the saved settings and reports whether it answered.</summary>
    public async Task<AiTestResultDto> TestAsync(StaffCaller caller, CancellationToken ct)
    {
        LanguageService.EnsureStaff(caller);
        var runtime = await GetRuntimeAsync(ct);
        if (!runtime.IsConfigured) return new AiTestResultDto(false, "ai.test.notConfigured", null, null);
        return await tester.TestAsync(runtime, ct);
    }

    public async Task<AiSettingsDto> GetAsync(StaffCaller caller, CancellationToken ct)
    {
        LanguageService.EnsureStaff(caller);
        return ToDto(await settings.GetAsync(ct));
    }

    public async Task<AiSettingsDto> UpdateAsync(StaffCaller caller, AiSettingsRequest request, CancellationToken ct)
    {
        LanguageService.EnsureStaff(caller);
        var current = await settings.GetAsync(ct);
        current.Update(request.ProviderName ?? "", request.BaseUrl ?? "", request.Model ?? "", request.Temperature,
            request.TimeoutSeconds, request.ChatEnabled, request.ReviewEnabled, request.MaxToolIterations,
            request.MaxHistoryMessages, request.ChatInstructions, request.ReviewInstructions,
            Custom(request.ChatPrompt, AiPrompts.DefaultChat), Custom(request.ReviewPrompt, AiPrompts.DefaultReview), caller.Name);
        if (request.ApiKey is not null)
            current.SetProtectedApiKey(request.ApiKey.Length == 0 ? null : protector.Protect(request.ApiKey.Trim()), caller.Name);
        await settings.SaveChangesAsync(ct);
        return ToDto(current);
    }

    /// <summary>The settings in force, falling back to configuration for anything not set in the back office.</summary>
    public async Task<AiRuntimeSettingsDto> GetRuntimeAsync(CancellationToken ct)
    {
        var s = await settings.GetAsync(ct);
        var key = s.ProtectedApiKey is null ? defaults.ApiKey : protector.Unprotect(s.ProtectedApiKey);
        return new AiRuntimeSettingsDto(s.ProviderName, string.IsNullOrWhiteSpace(s.BaseUrl) ? defaults.BaseUrl ?? "" : s.BaseUrl,
            string.IsNullOrWhiteSpace(s.Model) ? defaults.Model ?? "" : s.Model, key, s.Temperature, s.TimeoutSeconds,
            s.ChatEnabled, s.ReviewEnabled, s.MaxToolIterations, s.MaxHistoryMessages, s.ChatInstructions, s.ReviewInstructions,
            s.ChatPrompt ?? AiPrompts.DefaultChat, s.ReviewPrompt ?? AiPrompts.DefaultReview);
    }

    /// <summary>Staff's prompt, or null when it is empty or the same as the default (so later default updates apply).</summary>
    private static string? Custom(string? prompt, string defaultPrompt) =>
        string.IsNullOrWhiteSpace(prompt) || Normalise(prompt) == Normalise(defaultPrompt) ? null : prompt;

    private static string Normalise(string text) => text.Replace("\r\n", "\n").Trim();

    private AiSettingsDto ToDto(AiSettings s) => new(s.ProviderName,
        string.IsNullOrWhiteSpace(s.BaseUrl) ? defaults.BaseUrl ?? "" : s.BaseUrl,
        string.IsNullOrWhiteSpace(s.Model) ? defaults.Model ?? "" : s.Model,
        s.HasApiKey || !string.IsNullOrEmpty(defaults.ApiKey), s.Temperature, s.TimeoutSeconds, s.ChatEnabled, s.ReviewEnabled,
        s.MaxToolIterations, s.MaxHistoryMessages, s.ChatInstructions, s.ReviewInstructions,
        s.ChatPrompt, s.ReviewPrompt, AiPrompts.DefaultChat, AiPrompts.DefaultReview,
        s.IsConfigured || (!string.IsNullOrWhiteSpace(defaults.BaseUrl) && !string.IsNullOrWhiteSpace(defaults.Model)),
        s.UpdatedAt, s.UpdatedBy);
}

/// <summary>The languages and texts shipped with a release, added to the database on start-up.</summary>
public sealed record SeedLanguage(string Code, LocalizedText Name, string Direction, bool IsDefault, int SortOrder);

public sealed record SeedData(IReadOnlyList<SeedLanguage> Languages, IReadOnlyDictionary<string, LocalizedText> Strings);

/// <summary>
/// First start: creates the shipped languages (the shipped default becomes the default) and their texts.
/// Every start: adds keys that are new in this release, without touching texts staff have edited.
/// </summary>
public sealed class PlatformSeeder(ILanguageRepository languages, ITranslationRepository translations, IAiSettingsRepository ai)
{
    public async Task SeedAsync(SeedData seed, CancellationToken ct)
    {
        var existing = await languages.ListAsync(ct);
        if (existing.Count == 0)
        {
            foreach (var s in seed.Languages)
            {
                var language = Language.Create(s.Code, s.Name, s.Direction, s.SortOrder, "Platform");
                language.Enable("Platform");
                await languages.AddAsync(language, ct);
            }
            await languages.SaveChangesAsync(ct);
            existing = await languages.ListAsync(ct);
            var chosen = existing.First(l => seed.Languages.Any(s => s.IsDefault && s.Code == l.Code));
            LanguagePolicy.SetDefault(existing, chosen, "Platform");
        }

        var present = (await translations.ListAsync(null, ct)).Select(t => (t.Key, t.LanguageCode)).ToHashSet();
        foreach (var (key, texts) in seed.Strings)
            foreach (var language in existing)
                if (texts.Has(language.Code) && !present.Contains((key, language.Code)))
                    await translations.AddAsync(Translation.Create(key, language.Code, texts[language.Code], "Platform"), ct);

        await ai.GetAsync(ct); // creates the settings record on first start
        await languages.SaveChangesAsync(ct);
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddPlatformApplication(this IServiceCollection services) =>
        services.AddScoped<LanguageService>()
            .AddScoped<TranslationService>()
            .AddScoped<AiSettingsService>()
            .AddScoped<PlatformSeeder>();
}
