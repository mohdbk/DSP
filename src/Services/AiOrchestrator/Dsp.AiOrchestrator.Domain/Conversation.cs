namespace Dsp.AiOrchestrator.Domain;

public static class ChatRoles
{
    public const string User = "user";
    public const string Assistant = "assistant";
}

/// <summary>One message in a service conversation. The client holds the history; the server is stateless.</summary>
public sealed record ChatMessage(string Role, string Content);

/// <summary>
/// Guardrails for the agent, kept in one place so they can be reviewed. Limits come from the AI settings; the words
/// that confirm a submission come from the message catalogue, so they exist in every language.
/// </summary>
public static class AgentPolicy
{
    /// <summary>The agent submits only after the user explicitly confirms; it never pays or uploads on their behalf.</summary>
    public static bool Matches(string text, IEnumerable<string> words)
    {
        var t = Normalize(text);
        return words.Select(Normalize).Any(w => w.Length > 0 && w == t);
    }

    /// <summary>Splits a catalogue entry like "submit|confirm|send it" into words.</summary>
    public static IReadOnlyList<string> Words(string entry) =>
        entry.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string Normalize(string text) => text.Trim().TrimEnd('.', '!', '؟', '?').ToLowerInvariant();
}
