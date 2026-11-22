using System.Text.Json;
using Dsp.AiOrchestrator.Domain;
using Microsoft.Extensions.Logging;

namespace Dsp.AiOrchestrator.Application;

/// <param name="Language">"en" or "ar": the language the assistant replies in.</param>
public sealed record ChatRequest(Guid ApplicationId, IReadOnlyList<ChatMessage> Messages, string? PendingField,
    string? Language = null)
{
    public string Lang => Language is not null && Language.StartsWith("ar", StringComparison.OrdinalIgnoreCase) ? "ar" : "en";
}

public sealed record ChatResponse(string Reply, string? PendingField, ApplicationView Application, string Mode);

/// <summary>
/// The chat view of an e-service. Uses an LLM with tools when configured, otherwise a guided
/// step-by-step conversation. Both call the same E-Services API as the form view.
/// </summary>
public sealed class ChatService(IEServicesApi eservices, ILlmClient llm, GuidedConversation guided,
    ILogger<ChatService> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<ChatResponse> ChatAsync(ChatRequest request, CancellationToken ct)
    {
        var application = await eservices.GetApplicationAsync(request.ApplicationId, ct);
        var service = await eservices.GetServiceAsync(application.ServiceCode, ct);

        if (application.Status != "Draft")
        {
            return new ChatResponse(request.Lang == "ar"
                    ? $"تم تقديم الطلب {application.Number} بالفعل. يمكنك متابعته من صفحة طلباتي."
                    : $"Application {application.Number} is already {Humanize(application.Status)}. You can follow it from My applications.",
                null, application, llm.IsConfigured ? "llm" : "guided");
        }

        return llm.IsConfigured
            ? await RunAgentAsync(request, service, application, ct)
            : await guided.RespondAsync(request, service, application, ct);
    }

    private async Task<ChatResponse> RunAgentAsync(ChatRequest request, ServiceView service,
        ApplicationView application, CancellationToken ct)
    {
        var messages = new List<LlmMessage> { new("system", SystemPrompt(service, application, request.Lang)) };
        messages.AddRange(request.Messages.TakeLast(AgentPolicy.MaxHistoryMessages)
            .Where(m => m.Role is ChatRoles.User or ChatRoles.Assistant)
            .Select(m => new LlmMessage(m.Role, m.Content)));
        if (request.Messages.Count == 0)
            messages.Add(new LlmMessage(ChatRoles.User, request.Lang == "ar"
                ? "مرحبًا، أريد بدء هذا الطلب."
                : "Hello, I want to start this application."));

        for (var i = 0; i < AgentPolicy.MaxToolIterations; i++)
        {
            var reply = await llm.CompleteAsync(messages, Tools, ct);
            if (reply.ToolCalls.Count == 0)
            {
                application = await eservices.GetApplicationAsync(application.Id, ct);
                return new ChatResponse(reply.Content ?? "", null, application, "llm");
            }

            messages.Add(new LlmMessage(ChatRoles.Assistant, reply.Content, ToolCalls: reply.ToolCalls));
            foreach (var call in reply.ToolCalls)
            {
                var result = await ExecuteToolAsync(call, application.Id, ct);
                messages.Add(new LlmMessage("tool", result, ToolCallId: call.Id));
            }
        }

        logger.LogWarning("Agent hit the tool iteration limit for {ApplicationId}", application.Id);
        application = await eservices.GetApplicationAsync(application.Id, ct);
        return new ChatResponse(request.Lang == "ar"
                ? "حفظت ما استطعت. يرجى استخدام النموذج لإكمال الحقول المتبقية."
                : "I saved what I could. Please check the form view to finish the remaining fields.",
            null, application, "llm");
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

    private static string SystemPrompt(ServiceView service, ApplicationView application, string lang) => $"""
        You are the MoET E-Services assistant. You help the user complete the "{service.Name}" application
        (number {application.Number}) by conversation instead of a form.
        Reply in {(lang == "ar" ? "Arabic (Modern Standard Arabic, clear and polite). Use the Arabic labels (labelAr) of fields" : "English")}.

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

    internal static string Humanize(string status) => status switch
    {
        "UnderReview" => "under review",
        "AwaitingPayment" => "awaiting payment",
        _ => status.ToLowerInvariant(),
    };
}
