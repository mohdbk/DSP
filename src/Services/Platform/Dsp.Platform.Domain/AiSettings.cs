using Dsp.SharedKernel;

namespace Dsp.Platform.Domain;

/// <summary>
/// How the platform reaches its AI model and what the AI may do. One record, edited in the back office.
/// The API key is stored encrypted and never shown again after it is set.
/// </summary>
public sealed class AiSettings : Entity
{
    public static readonly Guid SingletonId = new("6f1e1a52-2c0d-4b39-9a37-5a1f1d0a0001");

    private AiSettings() { }

    /// <summary>A label for people, e.g. "Net0".</summary>
    public string ProviderName { get; private set; } = "";

    /// <summary>OpenAI-compatible endpoint, e.g. https://api.example.ae/v1.</summary>
    public string BaseUrl { get; private set; } = "";
    public string Model { get; private set; } = "";
    public string? ProtectedApiKey { get; private set; }
    public double Temperature { get; private set; } = 0.2;
    public int TimeoutSeconds { get; private set; } = 90;

    /// <summary>The chat view uses the model (otherwise the guided assistant).</summary>
    public bool ChatEnabled { get; private set; } = true;

    /// <summary>The AI pre-review uses the model (otherwise the built-in checks alone).</summary>
    public bool ReviewEnabled { get; private set; } = true;

    public int MaxToolIterations { get; private set; } = 6;
    public int MaxHistoryMessages { get; private set; } = 30;

    /// <summary>Extra instructions added to the chat assistant's prompt.</summary>
    public string? ChatInstructions { get; private set; }

    /// <summary>Extra instructions added to the pre-review prompt.</summary>
    public string? ReviewInstructions { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
    public string UpdatedBy { get; private set; } = "";

    public bool HasApiKey => !string.IsNullOrEmpty(ProtectedApiKey);
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Model);

    public static AiSettings CreateDefault() => new() { Id = SingletonId, UpdatedAt = DateTimeOffset.UtcNow, UpdatedBy = "Platform" };

    public void Update(string providerName, string baseUrl, string model, double temperature, int timeoutSeconds,
        bool chatEnabled, bool reviewEnabled, int maxToolIterations, int maxHistoryMessages,
        string? chatInstructions, string? reviewInstructions, string by)
    {
        var url = (baseUrl ?? "").Trim();
        if (url.Length > 0 && (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")))
            throw new ValidationException([new FieldError("baseUrl", "errors.ai.baseUrl")]);
        if (temperature is < 0 or > 2) throw new ValidationException([new FieldError("temperature", "errors.ai.temperature")]);
        if (timeoutSeconds is < 5 or > 600) throw new ValidationException([new FieldError("timeoutSeconds", "errors.ai.timeout")]);
        if (maxToolIterations is < 1 or > 20) throw new ValidationException([new FieldError("maxToolIterations", "errors.ai.iterations")]);
        if (maxHistoryMessages is < 2 or > 200) throw new ValidationException([new FieldError("maxHistoryMessages", "errors.ai.history")]);

        ProviderName = (providerName ?? "").Trim();
        BaseUrl = url.TrimEnd('/');
        Model = (model ?? "").Trim();
        Temperature = temperature;
        TimeoutSeconds = timeoutSeconds;
        ChatEnabled = chatEnabled;
        ReviewEnabled = reviewEnabled;
        MaxToolIterations = maxToolIterations;
        MaxHistoryMessages = maxHistoryMessages;
        ChatInstructions = string.IsNullOrWhiteSpace(chatInstructions) ? null : chatInstructions.Trim();
        ReviewInstructions = string.IsNullOrWhiteSpace(reviewInstructions) ? null : reviewInstructions.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = by;
    }

    /// <summary>Stores a new key (already encrypted), or removes it with null.</summary>
    public void SetProtectedApiKey(string? protectedKey, string by)
    {
        ProtectedApiKey = string.IsNullOrEmpty(protectedKey) ? null : protectedKey;
        UpdatedAt = DateTimeOffset.UtcNow;
        UpdatedBy = by;
    }
}
