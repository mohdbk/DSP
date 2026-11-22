using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dsp.AiOrchestrator.Application;
using Microsoft.Extensions.Logging;

namespace Dsp.AiOrchestrator.Infrastructure;

public sealed class LlmOptions
{
    /// <summary>OpenAI-compatible base URL, e.g. https://api.example.com/v1 (Net0 in production).</summary>
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public double Temperature { get; set; } = 0.2;
}

/// <summary>Chat completions with tool calling over any OpenAI-compatible endpoint.</summary>
public sealed class OpenAiCompatibleLlmClient(HttpClient http, LlmOptions options, ILogger<OpenAiCompatibleLlmClient> logger)
    : ILlmClient
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.BaseUrl) && !string.IsNullOrWhiteSpace(options.Model);
    public string? Model => options.Model;

    public async Task<LlmReply> CompleteAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmTool> tools,
        CancellationToken ct)
    {
        var body = new JsonObject
        {
            ["model"] = options.Model,
            ["temperature"] = options.Temperature,
            ["messages"] = new JsonArray(messages.Select(ToJson).ToArray<JsonNode?>()),
        };
        if (tools.Count > 0)
        {
            // Some OpenAI-compatible servers reject an empty tools list, so only send it when there are tools.
            body["tools"] = new JsonArray(tools.Select(t => (JsonNode?)new JsonObject
            {
                ["type"] = "function",
                ["function"] = new JsonObject
                {
                    ["name"] = t.Name,
                    ["description"] = t.Description,
                    ["parameters"] = JsonNode.Parse(t.ParametersJsonSchema),
                },
            }).ToArray());
            body["tool_choice"] = "auto";
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{options.BaseUrl!.TrimEnd('/')}/chat/completions")
        {
            Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);

        using var response = await http.SendAsync(request, ct);
        var text = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("LLM call failed: {Status} {Body}", (int)response.StatusCode, text.Length > 500 ? text[..500] : text);
            throw new HttpRequestException($"LLM returned {(int)response.StatusCode}");
        }

        var message = JsonNode.Parse(text)?["choices"]?[0]?["message"]
                      ?? throw new InvalidOperationException("LLM response had no message.");
        var calls = message["tool_calls"]?.AsArray()
            .Select(c => new LlmToolCall(
                c!["id"]!.GetValue<string>(),
                c["function"]!["name"]!.GetValue<string>(),
                c["function"]!["arguments"]?.GetValue<string>() ?? "{}"))
            .ToList() ?? [];

        return new LlmReply(message["content"]?.GetValue<string>(), calls);
    }

    private static JsonNode ToJson(LlmMessage m)
    {
        var node = new JsonObject { ["role"] = m.Role, ["content"] = m.Content };
        if (m.ToolCallId is not null) node["tool_call_id"] = m.ToolCallId;
        if (m.ToolCalls is { Count: > 0 })
        {
            node["tool_calls"] = new JsonArray(m.ToolCalls.Select(c => (JsonNode?)new JsonObject
            {
                ["id"] = c.Id,
                ["type"] = "function",
                ["function"] = new JsonObject { ["name"] = c.Name, ["arguments"] = c.ArgumentsJson },
            }).ToArray());
        }
        return node;
    }
}
