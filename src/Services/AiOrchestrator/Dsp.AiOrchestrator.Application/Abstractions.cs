using Dsp.Contracts;
using Dsp.SharedKernel;

namespace Dsp.AiOrchestrator.Application;

// The agent's view of E-Services. It only ever calls the public E-Services API, with the user's own token,
// so it can do nothing the user could not do in the form view. Texts come in every language the service has.

public sealed record FieldOptionView(string Value, LocalizedText Label);

public sealed record FieldView(string Key, LocalizedText Label, string Type, bool Required, LocalizedText? Help,
    IReadOnlyList<FieldOptionView>? Options);

public sealed record StepView(string Id, LocalizedText Title, LocalizedText? Description, IReadOnlyList<FieldView> Fields);

public sealed record ServiceView(string Code, LocalizedText Name, LocalizedText Description, decimal Fee, string Currency,
    IReadOnlyList<StepView> Steps)
{
    public IEnumerable<FieldView> AllFields => Steps.SelectMany(s => s.Fields);
}

public sealed record ApplicationView(Guid Id, string Number, string ServiceCode, LocalizedText ServiceName, string Status,
    Dictionary<string, string?> Data);

public sealed record FieldErrorView(string Field, string Message);

public sealed record SaveOutcome(ApplicationView Application, IReadOnlyList<FieldErrorView> Errors);

public sealed record SubmitOutcome(ApplicationView? Application, IReadOnlyList<FieldErrorView> Errors);

public interface IEServicesApi
{
    Task<ApplicationView> GetApplicationAsync(Guid id, CancellationToken ct);
    Task<ServiceView> GetServiceAsync(string code, CancellationToken ct);
    Task<SaveOutcome> SaveDataAsync(Guid id, IReadOnlyDictionary<string, string?> values, CancellationToken ct);
    Task<SubmitOutcome> SubmitAsync(Guid id, CancellationToken ct);
}

/// <summary>
/// The AI settings in force: edited by staff in the back office (Platform service), with environment variables as
/// the fallback. Nothing about the model (vendor, address, name, key, limits, instructions) is fixed in code.
/// </summary>
public sealed record AiSettings(string ProviderName, string BaseUrl, string Model, string? ApiKey, double Temperature,
    int TimeoutSeconds, bool Paused, int MaxToolIterations, int MaxHistoryMessages,
    string? ChatInstructions, string? ReviewInstructions, string? ChatPrompt = null, string? ReviewPrompt = null)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(Model);

    /// <summary>The editable part of the chat prompt in force (placeholders not filled in).</summary>
    public string ChatPromptOrDefault => string.IsNullOrWhiteSpace(ChatPrompt) ? AiPrompts.DefaultChat : ChatPrompt;

    /// <summary>The editable part of the pre-review prompt in force (placeholders not filled in).</summary>
    public string ReviewPromptOrDefault => string.IsNullOrWhiteSpace(ReviewPrompt) ? AiPrompts.DefaultReview : ReviewPrompt;

    /// <summary>The model is used whenever it is configured, unless staff paused the AI (emergency stop).</summary>
    public bool ChatActive => IsConfigured && !Paused;
    public bool ReviewActive => IsConfigured && !Paused;
}

public interface IAiSettingsProvider
{
    Task<AiSettings> GetAsync(CancellationToken ct);
}

// Model-agnostic LLM port. Infrastructure maps it to an OpenAI-compatible endpoint (Net0 in production).

public sealed record LlmToolCall(string Id, string Name, string ArgumentsJson);

public sealed record LlmMessage(string Role, string? Content, string? ToolCallId = null,
    IReadOnlyList<LlmToolCall>? ToolCalls = null);

public sealed record LlmTool(string Name, string Description, string ParametersJsonSchema);

public sealed record LlmReply(string? Content, IReadOnlyList<LlmToolCall> ToolCalls);

public interface ILlmClient
{
    Task<LlmReply> CompleteAsync(AiSettings settings, IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmTool> tools,
        CancellationToken ct);
}

/// <summary>Renders a message into every enabled language at once (texts stored for readers of any language).</summary>
public sealed class MultilingualRenderer
{
    private readonly IReadOnlyList<(string Code, IMessages Messages)> _languages;

    private MultilingualRenderer(IReadOnlyList<(string, IMessages)> languages) => _languages = languages;

    public IReadOnlyList<string> Languages => _languages.Select(l => l.Code).ToList();

    public static async Task<MultilingualRenderer> CreateAsync(ILocalization localization, IEnumerable<string>? only,
        CancellationToken ct)
    {
        var settings = await localization.GetSettingsAsync(ct);
        var codes = settings.Enabled.OrderByDescending(l => l.IsDefault).Select(l => l.Code).ToList();
        if (only is not null)
        {
            var wanted = only.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var filtered = codes.Where(wanted.Contains).ToList();
            if (filtered.Count > 0) codes = filtered;
        }
        var list = new List<(string, IMessages)>();
        foreach (var code in codes) list.Add((code, await localization.GetMessagesAsync(code, ct)));
        return new MultilingualRenderer(list);
    }

    public LocalizedText Render(MessageRef message) =>
        new(_languages.Select(l => KeyValuePair.Create(l.Code, message.Render(l.Messages))));
}
