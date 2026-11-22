namespace Dsp.AiOrchestrator.Application;

// The agent's view of E-Services. It only ever calls the public E-Services API, with the user's own token,
// so it can do nothing the user could not do in the form view.

public sealed record FieldOptionView(string Value, string Label);

public sealed record FieldView(string Key, string Label, string Type, bool Required, string? Help,
    IReadOnlyList<FieldOptionView>? Options);

public sealed record StepView(string Id, string Title, string? Description, IReadOnlyList<FieldView> Fields);

public sealed record ServiceView(string Code, string Name, string Description, decimal Fee, string Currency,
    IReadOnlyList<StepView> Steps)
{
    public IEnumerable<FieldView> AllFields => Steps.SelectMany(s => s.Fields);
}

public sealed record ApplicationView(Guid Id, string Number, string ServiceCode, string ServiceName, string Status,
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

// Model-agnostic LLM port. Infrastructure maps it to an OpenAI-compatible endpoint (Net0 in production).

public sealed record LlmToolCall(string Id, string Name, string ArgumentsJson);

public sealed record LlmMessage(string Role, string? Content, string? ToolCallId = null,
    IReadOnlyList<LlmToolCall>? ToolCalls = null);

public sealed record LlmTool(string Name, string Description, string ParametersJsonSchema);

public sealed record LlmReply(string? Content, IReadOnlyList<LlmToolCall> ToolCalls);

public interface ILlmClient
{
    bool IsConfigured { get; }
    string? Model { get; }
    Task<LlmReply> CompleteAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmTool> tools, CancellationToken ct);
}
