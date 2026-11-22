using System.Text.Json;
using Dsp.AiOrchestrator.Domain;
using Dsp.SharedKernel;
using Microsoft.Extensions.Logging;

namespace Dsp.AiOrchestrator.Application;

/// <summary>A chat turn. The reply language is the caller's (Accept-Language, resolved against the enabled languages).</summary>
public sealed record ChatRequest(Guid ApplicationId, IReadOnlyList<ChatMessage> Messages, string? PendingField);

public sealed record ChatResponse(string Reply, string? PendingField, ApplicationView Application, string Mode);

/// <summary>
/// The chat view of an e-service. Uses a model with tools when one is configured and chat is switched on in the
/// AI settings, otherwise a guided step-by-step conversation. Both call the same E-Services API as the form view.
/// </summary>
public sealed class ChatService(IEServicesApi eservices, ILlmClient llm, IAiSettingsProvider settingsProvider,
    GuidedConversation guided, IMessages messages, ILocalization localization, ILogger<ChatService> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct)
    {
        var application = await eservices.GetApplicationAsync(request.ApplicationId, ct);
        var service = await eservices.GetServiceAsync(application.ServiceCode, ct);
        var settings = await settingsProvider.GetAsync(ct);

        if (application.Status != "Draft")
        {
            return new ChatResponse(messages.Format("chat.alreadySubmitted", ("number", application.Number),
                    ("status", messages.Format($"status.{application.Status}"))),
                null, application, settings.ChatActive ? "llm" : "guided");
        }

        return settings.ChatActive
            ? await RunAgentAsync(request, service, application, settings, ct)
            : await guided.RespondAsync(request, service, application, ct);
    }

    private async Task<ChatResponse> RunAgentAsync(ChatRequest request, ServiceView service,
        ApplicationView application, AiSettings settings, CancellationToken ct)
    {
        var languageName = (await localization.GetSettingsAsync(ct)).Languages
            .FirstOrDefault(l => string.Equals(l.Code, messages.Language, StringComparison.OrdinalIgnoreCase))?.Name.Get("en")
            ?? messages.Language;
        var history = new List<LlmMessage> { new("system", SystemPrompt(service, application, languageName, settings)) };
        history.AddRange(request.Messages.TakeLast(settings.MaxHistoryMessages)
            .Where(m => m.Role is ChatRoles.User or ChatRoles.Assistant)
            .Select(m => new LlmMessage(m.Role, m.Content)));
        if (request.Messages.Count == 0)
            history.Add(new LlmMessage(ChatRoles.User, messages.Format("chat.start")));

        for (var i = 0; i < settings.MaxToolIterations; i++)
        {
            var reply = await llm.CompleteAsync(settings, history, Tools, ct);
            if (reply.ToolCalls.Count == 0)
            {
                application = await eservices.GetApplicationAsync(application.Id, ct);
                return new ChatResponse(reply.Content ?? "", null, application, "llm");
            }

            history.Add(new LlmMessage(ChatRoles.Assistant, reply.Content, ToolCalls: reply.ToolCalls));
            foreach (var call in reply.ToolCalls)
            {
                var result = await ExecuteToolAsync(call, application.Id, ct);
                history.Add(new LlmMessage("tool", result, ToolCallId: call.Id));
            }
        }

        logger.LogWarning("Agent hit the tool iteration limit for {ApplicationId}", application.Id);
        application = await eservices.GetApplicationAsync(application.Id, ct);
        return new ChatResponse(messages.Format("chat.toolLimit"), null, application, "llm");
    }

    private async Task<string> ExecuteToolAsync(LlmToolCall call, Guid applicationId, CancellationToken ct)
    {
        try
        {
            switch (call.Name)
            {
                case "save_fields":
                {
                    using var args = JsonDocument.Parse(string.IsNullOrWhiteSpace(call.ArgumentsJson) ? "{}" : call.ArgumentsJson);
                    var values = new Dictionary<string, string?>();
                    if (args.RootElement.TryGetProperty("values", out var v) && v.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var p in v.EnumerateObject())
                            values[p.Name] = p.Value.ValueKind == JsonValueKind.String ? p.Value.GetString() : p.Value.ToString();
                    }
                    var outcome = await eservices.SaveDataAsync(applicationId, values, ct);
                    return JsonSerializer.Serialize(new { saved = values.Keys.Except(outcome.Errors.Select(e => e.Field)), errors = outcome.Errors, data = outcome.Application.Data }, Json);
                }
                case "submit_application":
                {
                    var outcome = await eservices.SubmitAsync(applicationId, ct);
                    return outcome.Errors.Count > 0
                        ? JsonSerializer.Serialize(new { submitted = false, errors = outcome.Errors }, Json)
                        : JsonSerializer.Serialize(new { submitted = true, number = outcome.Application!.Number, status = outcome.Application.Status }, Json);
                }
                case "get_application":
                {
                    var app = await eservices.GetApplicationAsync(applicationId, ct);
                    return JsonSerializer.Serialize(new { app.Number, app.Status, app.Data }, Json);
                }
                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown tool {call.Name}" }, Json);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Tool {Tool} failed", call.Name);
            return JsonSerializer.Serialize(new { error = "The tool call failed. Tell the user and suggest the form view." }, Json);
        }
    }

    private string SystemPrompt(ServiceView service, ApplicationView application, string languageName, AiSettings settings) => $"""
        You are the E-Services assistant of the UAE Ministry of Economy and Tourism. You help the user complete the
        "{messages.Pick(service.Name)}" application (number {application.Number}) by conversation instead of a form.
        Reply in {languageName} (language code "{messages.Language}"), clear and polite. Labels, options and help
        texts below are given per language code: use the ones for "{messages.Language}" (or "{messages.DefaultLanguage}" if missing).

        How to work:
        - Ask for one or two missing items at a time, in the order of the steps. Be brief, warm and clear.
        - When the user gives a value, call save_fields right away. Use option VALUES (not labels) for select
          fields, YYYY-MM-DD for dates, and "true" for confirmations.
        - If save_fields returns errors, explain them simply and ask again.
        - Values already in the data were pre-filled from the user's UAE PASS profile. Confirm them, don't re-ask.
        - Never invent or guess values. File uploads cannot be done in chat: ask the user to upload them in the form view.
        - When every required field is filled, show a short summary, state the fee ({service.Fee} {service.Currency},
          payable after approval) and ask the user to confirm. Call submit_application only after they explicitly confirm.
        - Only discuss this application. You cannot pay, approve or change anything else.
        {(string.IsNullOrWhiteSpace(settings.ChatInstructions) ? "" : "\nAdditional instructions:\n" + settings.ChatInstructions)}

        Service definition (steps and fields):
        {JsonSerializer.Serialize(service.Steps, Json)}

        Current application data:
        {JsonSerializer.Serialize(application.Data, Json)}
        """;

    private static readonly IReadOnlyList<LlmTool> Tools =
    [
        new("save_fields", "Save one or more field values to the application. Returns errors for invalid values.",
            """{"type":"object","properties":{"values":{"type":"object","description":"Map of field key to value","additionalProperties":{"type":"string"}}},"required":["values"]}"""),
        new("submit_application", "Submit the application. Only after the user explicitly confirms.",
            """{"type":"object","properties":{}}"""),
        new("get_application", "Read the current application data and status.",
            """{"type":"object","properties":{}}"""),
    ];
}
