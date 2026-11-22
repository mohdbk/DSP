using System.Text.Json;
using Dsp.AiOrchestrator.Domain;
using Dsp.SharedKernel;

namespace Dsp.AiOrchestrator.Application;

// The chat view over the AG-UI protocol (https://docs.ag-ui.com): a run takes the conversation and returns a stream of
// events — lifecycle, the assistant's text, state, and A2UI screens (https://a2ui.org, v1.0) carried as AG-UI activity
// events of type "a2ui". Screens are built from the service definition, never invented by the model, and use our own
// catalogue of AEGOV components (A2UiCatalog.Id). A user's action on a screen (save these answers, submit) comes back
// as the next run's forwardedProps.action and is carried out directly, without the model.

public sealed record AgUiMessage(string? Id, string Role, string? Content);

/// <summary>An action from an A2UI screen: the event name and its resolved context.</summary>
public sealed record AgUiAction(string Name, JsonElement? Context);

public sealed record AgUiForwarded(Guid ApplicationId, string? PendingField, AgUiAction? Action);

/// <summary>AG-UI RunAgentInput (the fields this agent uses).</summary>
public sealed record AgUiRunInput(string? ThreadId, string? RunId, IReadOnlyList<AgUiMessage>? Messages, AgUiForwarded ForwardedProps);

public static class AgUiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

public static class AgUiEvents
{
    public static Dictionary<string, object?> Of(string type, params (string Key, object? Value)[] fields)
    {
        var e = new Dictionary<string, object?> { ["type"] = type, ["timestamp"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
        foreach (var (key, value) in fields) e[key] = value;
        return e;
    }
}

/// <summary>Runs one chat turn and returns its AG-UI events in order.</summary>
public sealed class AgUiChatRunner(ChatService chat, IEServicesApi eservices, IMessages messages, A2UiScreens screens)
{
    public async Task<IReadOnlyList<Dictionary<string, object?>>> RunAsync(AgUiRunInput input, CancellationToken ct)
    {
        var threadId = input.ThreadId ?? input.ForwardedProps.ApplicationId.ToString();
        var runId = input.RunId ?? Guid.NewGuid().ToString();
        var events = new List<Dictionary<string, object?>> { AgUiEvents.Of("RUN_STARTED", ("threadId", threadId), ("runId", runId)) };
        try
        {
            var appId = input.ForwardedProps.ApplicationId;
            string reply;
            string? pendingField = null;
            var mode = "guided";
            IReadOnlyList<FieldErrorView> errors = [];

            if (input.ForwardedProps.Action is { } action)
            {
                events.Add(AgUiEvents.Of("STEP_STARTED", ("stepName", action.Name)));
                (reply, errors) = await ActAsync(appId, action, ct);
                events.Add(AgUiEvents.Of("STEP_FINISHED", ("stepName", action.Name)));
            }
            else
            {
                var history = (input.Messages ?? []).Where(m => m.Role is ChatRoles.User or ChatRoles.Assistant && !string.IsNullOrWhiteSpace(m.Content))
                    .Select(m => new ChatMessage(m.Role, m.Content!)).ToList();
                var response = await chat.ChatAsync(new ChatRequest(appId, history, input.ForwardedProps.PendingField), ct);
                (reply, pendingField, mode) = (response.Reply, response.PendingField, response.Mode);
            }

            AddText(events, reply);
            var application = await eservices.GetApplicationAsync(appId, ct);
            events.Add(AgUiEvents.Of("STATE_SNAPSHOT", ("snapshot", new
            {
                application = new { application.Id, application.Number, application.Status, application.Data },
                pendingField, mode,
            })));

            if (application.Status == "Draft")
            {
                var service = await eservices.GetServiceAsync(application.ServiceCode, ct);
                var screen = await screens.NextAsync(service, application, errors, ct);
                if (screen is not null)
                    events.Add(AgUiEvents.Of("ACTIVITY_SNAPSHOT", ("messageId", Guid.NewGuid().ToString()),
                        ("activityType", A2UiCatalog.ActivityType), ("content", new { messages = screen }), ("replace", true)));
            }

            events.Add(AgUiEvents.Of("RUN_FINISHED", ("threadId", threadId), ("runId", runId), ("outcome", new { type = "success" })));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            events.Add(AgUiEvents.Of("RUN_ERROR", ("message", messages.Format("chat.error")), ("code", ex.GetType().Name)));
        }
        return events;
    }

    /// <summary>A screen action, carried out directly: save the answers on the screen, or submit.</summary>
    private async Task<(string Reply, IReadOnlyList<FieldErrorView> Errors)> ActAsync(Guid appId, AgUiAction action, CancellationToken ct)
    {
        switch (action.Name)
        {
            case A2UiCatalog.SaveAction:
            {
                var values = new Dictionary<string, string?>();
                if (action.Context is { ValueKind: JsonValueKind.Object } context &&
                    context.TryGetProperty("values", out var v) && v.ValueKind == JsonValueKind.Object)
                {
                    foreach (var p in v.EnumerateObject())
                        values[p.Name] = p.Value.ValueKind switch
                        {
                            JsonValueKind.String => p.Value.GetString(),
                            JsonValueKind.True => "true",
                            JsonValueKind.False => "false",
                            JsonValueKind.Null or JsonValueKind.Undefined => null,
                            _ => p.Value.ToString(),
                        };
                }
                var outcome = await eservices.SaveDataAsync(appId, values, ct);
                return outcome.Errors.Count > 0
                    ? (messages.Format("chat.a2ui.fix"), outcome.Errors)
                    : (messages.Format("chat.a2ui.saved"), []);
            }
            case A2UiCatalog.SubmitAction:
            {
                var outcome = await eservices.SubmitAsync(appId, ct);
                return outcome.Errors.Count > 0 || outcome.Application is null
                    ? (messages.Format("chat.a2ui.cannotSubmit", ("errors", string.Join("\n", outcome.Errors.Select(e => "• " + e.Message)))), outcome.Errors)
                    : (messages.Format("chat.a2ui.submitted", ("number", outcome.Application.Number)), []);
            }
            default:
                return (messages.Format("chat.a2ui.unknown"), []);
        }
    }

    /// <summary>The reply as an AG-UI text message, in a few deltas so it appears progressively.</summary>
    private static void AddText(List<Dictionary<string, object?>> events, string reply)
    {
        if (string.IsNullOrWhiteSpace(reply)) return;
        var id = Guid.NewGuid().ToString();
        events.Add(AgUiEvents.Of("TEXT_MESSAGE_START", ("messageId", id), ("role", "assistant")));
        const int size = 48;
        for (var i = 0; i < reply.Length; i += size)
            events.Add(AgUiEvents.Of("TEXT_MESSAGE_CONTENT", ("messageId", id), ("delta", reply.Substring(i, Math.Min(size, reply.Length - i)))));
        events.Add(AgUiEvents.Of("TEXT_MESSAGE_END", ("messageId", id)));
    }
}
