using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dsp.BuildingBlocks.Localization;
using Dsp.BuildingBlocks.Persistence;
using Dsp.Platform.Application;
using Dsp.Platform.Domain;
using Dsp.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dsp.Platform.Infrastructure;

/// <summary>The service model. Provider-specific subclasses carry their own migrations.</summary>
public abstract class PlatformDbContext(DbContextOptions options) : DbContext(options)
{
    public DbSet<Language> Languages => Set<Language>();
    public DbSet<Translation> Translations => Set<Translation>();
    public DbSet<AiSettings> AiSettings => Set<AiSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("platform");
        modelBuilder.Entity<Language>(e =>
        {
            e.ToTable("Languages");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(35);
            e.Property(x => x.Name).IsLocalizedText().HasMaxLength(4000);
            e.Property(x => x.Direction).HasMaxLength(3);
            e.Property(x => x.UpdatedBy).HasMaxLength(200);
        });
        modelBuilder.Entity<Translation>(e =>
        {
            e.ToTable("Translations");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.Key, x.LanguageCode }).IsUnique();
            e.HasIndex(x => x.LanguageCode);
            e.Property(x => x.Key).HasMaxLength(200);
            e.Property(x => x.LanguageCode).HasMaxLength(35);
            e.Property(x => x.Text).HasMaxLength(4000);
            e.Property(x => x.UpdatedBy).HasMaxLength(200);
        });
        modelBuilder.Entity<AiSettings>(e =>
        {
            e.ToTable("AiSettings");
            e.HasKey(x => x.Id);
            e.Property(x => x.ProviderName).HasMaxLength(100);
            e.Property(x => x.BaseUrl).HasMaxLength(500);
            e.Property(x => x.Model).HasMaxLength(200);
            e.Property(x => x.ProtectedApiKey).HasMaxLength(2000);
            e.Property(x => x.ChatInstructions).HasMaxLength(8000);
            e.Property(x => x.ReviewInstructions).HasMaxLength(8000);
            e.Property(x => x.ChatPrompt).HasMaxLength(Dsp.Platform.Domain.AiSettings.MaxPromptLength);
            e.Property(x => x.ReviewPrompt).HasMaxLength(Dsp.Platform.Domain.AiSettings.MaxPromptLength);
            e.Property(x => x.UpdatedBy).HasMaxLength(200);
        });
    }
}

public sealed class PlatformSqlServerDbContext(DbContextOptions<PlatformSqlServerDbContext> options) : PlatformDbContext(options);

public sealed class PlatformPostgresDbContext(DbContextOptions<PlatformPostgresDbContext> options) : PlatformDbContext(options);

/// <summary>Used by dotnet ef to create migrations; never opens a connection.</summary>
public sealed class PlatformSqlServerDesignTimeFactory : IDesignTimeDbContextFactory<PlatformSqlServerDbContext>
{
    public PlatformSqlServerDbContext CreateDbContext(string[] args) => new(DesignTimeOptions.SqlServer<PlatformSqlServerDbContext>());
}

/// <summary>Used by dotnet ef to create migrations; never opens a connection.</summary>
public sealed class PlatformPostgresDesignTimeFactory : IDesignTimeDbContextFactory<PlatformPostgresDbContext>
{
    public PlatformPostgresDbContext CreateDbContext(string[] args) => new(DesignTimeOptions.Postgres<PlatformPostgresDbContext>());
}

public sealed class LanguageRepository(PlatformDbContext db, PlatformCache cache) : ILanguageRepository
{
    public async Task<IReadOnlyList<Language>> ListAsync(CancellationToken ct) => await db.Languages.ToListAsync(ct);

    public Task<Language?> GetAsync(string code, CancellationToken ct) =>
        db.Languages.FirstOrDefaultAsync(l => l.Code == code.Trim(), ct);

    public async Task AddAsync(Language language, CancellationToken ct) => await db.Languages.AddAsync(language, ct);

    public void Remove(Language language) => db.Languages.Remove(language);

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        cache.Clear();
    }
}

public sealed class TranslationRepository(PlatformDbContext db) : ITranslationRepository
{
    public async Task<IReadOnlyList<Translation>> ListAsync(string? languageCode, CancellationToken ct) =>
        await (languageCode is null ? db.Translations : db.Translations.Where(t => t.LanguageCode == languageCode))
            .ToListAsync(ct);

    public Task<Translation?> GetAsync(string key, string languageCode, CancellationToken ct) =>
        db.Translations.FirstOrDefaultAsync(t => t.Key == key && t.LanguageCode == languageCode, ct);

    public async Task AddAsync(Translation translation, CancellationToken ct) => await db.Translations.AddAsync(translation, ct);

    public void Remove(Translation translation) => db.Translations.Remove(translation);

    public async Task RemoveLanguageAsync(string languageCode, CancellationToken ct) =>
        db.Translations.RemoveRange(await db.Translations.Where(t => t.LanguageCode == languageCode).ToListAsync(ct));

    public async Task RemoveKeyAsync(string key, CancellationToken ct) =>
        db.Translations.RemoveRange(await db.Translations.Where(t => t.Key == key).ToListAsync(ct));
}

public sealed class AiSettingsRepository(PlatformDbContext db, PlatformCache cache) : IAiSettingsRepository
{
    public async Task<AiSettings> GetAsync(CancellationToken ct)
    {
        var settings = await db.AiSettings.FirstOrDefaultAsync(s => s.Id == Domain.AiSettings.SingletonId, ct);
        if (settings is not null) return settings;
        settings = Domain.AiSettings.CreateDefault();
        await db.AiSettings.AddAsync(settings, ct);
        return settings;
    }

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        cache.Clear();
    }
}

/// <summary>Short-lived cache of languages and catalogues for this service's own requests; cleared on every change.</summary>
public sealed class PlatformCache
{
    private MemoryCache _cache = new(new MemoryCacheOptions());

    public async Task<T> GetAsync<T>(string key, Func<Task<T>> load)
    {
        if (_cache.TryGetValue(key, out T? value) && value is not null) return value;
        value = await load();
        _cache.Set(key, value, TimeSpan.FromSeconds(30));
        return value;
    }

    public void Clear()
    {
        var old = Interlocked.Exchange(ref _cache, new MemoryCache(new MemoryCacheOptions()));
        old.Dispose();
    }
}

/// <summary>The Platform service reads its own languages and texts directly instead of over HTTP.</summary>
public sealed class LocalLocalization(LanguageService languages, TranslationService translations, PlatformCache cache) : ILocalization
{
    public Task<LanguageSettings> GetSettingsAsync(CancellationToken ct) =>
        cache.GetAsync("settings", async () => new LanguageSettings((await languages.ListAllAsync(ct))
            .Select(l => new LanguageInfo(l.Code, l.Name, l.Direction, l.IsEnabled, l.IsDefault, l.SortOrder)).ToList()));

    public Task<IReadOnlyDictionary<string, string>> GetCatalogAsync(string language, CancellationToken ct) =>
        cache.GetAsync($"catalog:{language}", async () => (await translations.GetCatalogAsync(language, ct)).Strings);
}

/// <summary>
/// Encrypts secrets with AES-GCM under a key derived from <c>Platform:SecretKey</c>.
/// Changing the key makes stored secrets unreadable (they then count as not set and must be entered again).
/// </summary>
public sealed class AesSecretProtector : ISecretProtector
{
    private const string Prefix = "v1:";
    private readonly byte[] _key;

    public AesSecretProtector(IConfiguration configuration)
    {
        var secret = configuration["Platform:SecretKey"] ?? configuration["Jwt:Key"]
                     ?? throw new InvalidOperationException("Platform:SecretKey is required.");
        _key = SHA256.HashData(Encoding.UTF8.GetBytes("dsp-platform-secrets:" + secret));
    }

    public string Protect(string secret)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plain = Encoding.UTF8.GetBytes(secret);
        var cipher = new byte[plain.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_key, 16);
        aes.Encrypt(nonce, plain, cipher, tag);
        return Prefix + Convert.ToBase64String([.. nonce, .. tag, .. cipher]);
    }

    public string? Unprotect(string protectedSecret)
    {
        if (!protectedSecret.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        try
        {
            var data = Convert.FromBase64String(protectedSecret[Prefix.Length..]);
            var plain = new byte[data.Length - 28];
            using var aes = new AesGcm(_key, 16);
            aes.Decrypt(data.AsSpan(0, 12), data.AsSpan(28), data.AsSpan(12, 16), plain);
            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            return null;
        }
    }
}

/// <summary>The languages and texts shipped with this release (embedded <c>Seed/translations.json</c>).</summary>
public static class ShippedTexts
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static SeedData Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                               "Dsp.Platform.Infrastructure.Seed.translations.json")
                           ?? throw new InvalidOperationException("Seed/translations.json is not embedded.");
        return JsonSerializer.Deserialize<SeedData>(stream, Json) ?? throw new InvalidOperationException("Empty seed.");
    }
}

/// <summary>Where each key is used, from the "usage" block of the shipped texts.</summary>
public sealed class ShippedTextUsage(SeedData seed) : ITextUsage
{
    public IReadOnlyList<string> UsedIn(string key) =>
        seed.Usage is not null && seed.Usage.TryGetValue(key, out var where) ? where : [];
}

public static class DependencyInjection
{
    public static IServiceCollection AddPlatformInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDspDbContext<PlatformDbContext, PlatformSqlServerDbContext, PlatformPostgresDbContext>(configuration, "Platform");
        services.AddSingleton<PlatformCache>();
        services.AddSingleton<ITextUsage>(new ShippedTextUsage(ShippedTexts.Load()));
        services.AddScoped<ILanguageRepository, LanguageRepository>();
        services.AddScoped<ITranslationRepository, TranslationRepository>();
        services.AddScoped<IAiSettingsRepository, AiSettingsRepository>();
        services.AddSingleton<ISecretProtector, AesSecretProtector>();
        services.AddHttpClient(nameof(AiConnectionTester));
        services.AddScoped<IAiConnectionTester, AiConnectionTester>();
        services.AddSingleton(new AiDefaults(configuration["Llm:BaseUrl"], configuration["Llm:ApiKey"], configuration["Llm:Model"]));
        // Registered before AddDspLocalization, which then keeps this one instead of the HTTP client.
        services.AddScoped<ILocalization, LocalLocalization>();
        return services;
    }
}

/// <summary>Sends a one-word chat completion to the configured OpenAI-compatible endpoint.</summary>
public sealed class AiConnectionTester(IHttpClientFactory httpFactory) : IAiConnectionTester
{
    public async Task<AiTestResultDto> TestAsync(AiRuntimeSettingsDto settings, CancellationToken ct)
    {
        var http = httpFactory.CreateClient(nameof(AiConnectionTester));
        http.Timeout = TimeSpan.FromSeconds(Math.Clamp(settings.TimeoutSeconds, 5, 60));
        using var request = new HttpRequestMessage(HttpMethod.Post, settings.BaseUrl.TrimEnd('/') + "/chat/completions")
        {
            Content = System.Net.Http.Json.JsonContent.Create(new
            {
                model = settings.Model,
                messages = new[] { new { role = "user", content = "Reply with the word OK." } },
                max_tokens = 5,
            }),
        };
        if (!string.IsNullOrEmpty(settings.ApiKey))
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", settings.ApiKey);

        var started = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            using var response = await http.SendAsync(request, ct);
            var ms = (int)started.ElapsedMilliseconds;
            if (response.IsSuccessStatusCode) return new AiTestResultDto(true, "ai.test.ok", settings.Model, ms);
            var body = await response.Content.ReadAsStringAsync(ct);
            return new AiTestResultDto(false, "ai.test.rejected",
                $"HTTP {(int)response.StatusCode}: {(body.Length > 300 ? body[..300] : body)}", ms);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or UriFormatException or InvalidOperationException)
        {
            return new AiTestResultDto(false, "ai.test.unreachable", ex.Message, (int)started.ElapsedMilliseconds);
        }
    }
}
