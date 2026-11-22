namespace Dsp.AiOrchestrator.Domain;

public static class ChatRoles
{
    public const string User = "user";
    public const string Assistant = "assistant";
}

/// <summary>One message in a service conversation. The client holds the history; the server is stateless.</summary>
public sealed record ChatMessage(string Role, string Content);

/// <summary>Guardrails for the agent, kept in one place so they can be reviewed.</summary>
public static class AgentPolicy
{
    /// <summary>Most tool round-trips per user message, to bound cost and loops.</summary>
    public const int MaxToolIterations = 6;

    /// <summary>Most history messages sent to the model.</summary>
    public const int MaxHistoryMessages = 30;

    /// <summary>The agent submits only after the user explicitly confirms; it never pays or uploads on their behalf.</summary>
    public static readonly string[] ConfirmationWords =
        ["submit", "confirm", "yes submit", "send it", "go ahead", "إرسال", "ارسال", "أرسل", "ارسل", "تأكيد", "قدّم", "قدم"];

    public static bool IsConfirmation(string text) =>
        ConfirmationWords.Contains(text.Trim().ToLowerInvariant().TrimEnd('.', '!'));

    public static bool IsAffirmative(string text)
    {
        var t = text.Trim().ToLowerInvariant().TrimEnd('.', '!');
        return t is "yes" or "y" or "yeah" or "yep" or "ok" or "okay" or "agree" or "i agree" or "i confirm"
            or "confirm" or "confirmed" or "true" or "نعم" or "موافق" or "أوافق" or "اوافق" or "أؤكد" or "اؤكد";
    }
}
