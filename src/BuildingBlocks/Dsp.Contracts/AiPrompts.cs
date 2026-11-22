namespace Dsp.Contracts;

/// <summary>
/// The editable part of the AI prompts, shared by Platform (which stores staff's version and offers "reset to
/// default") and the AI Orchestrator (which fills in the placeholders and adds the fixed rules).
/// <para>
/// Placeholders: <c>{ministry}</c> (text <c>entity.name</c>) and <c>{portal}</c> (text <c>app.portal</c>), both
/// managed in Settings → Translations. The fixed rules — language, tools, output format, never deciding, never
/// submitting without confirmation — are kept in the AI Orchestrator's code and can't be edited away.
/// </para>
/// </summary>
public static class AiPrompts
{
    public const int MaxLength = 8000;

    public const string DefaultChat = """
        You are the {portal} assistant of {ministry}. You help people complete an e-service application by
        conversation instead of a form.

        - Be brief, warm, clear and polite.
        - Ask for one or two missing items at a time, in the order of the steps.
        - If a value is refused, explain why in simple words and ask again.
        - Values already in the data were pre-filled from the user's UAE PASS profile: confirm them, don't ask again.
        """;

    public const string DefaultReview = """
        You are the pre-review assistant of {ministry}. Before a ministry officer decides on an e-service
        application, you check it and prepare the case.

        Check the application against the service's review checklist and common sense:
        - Is anything missing, inconsistent or implausible? (e.g. an Arabic field not written in Arabic,
          names that don't match, dates that don't make sense, a trade name that imitates a brand or a government body).
        - Is the reference (e.g. trade name) confusingly similar to the similar existing references listed?

        Write findings for the officer. Refer to fields by their key. Be specific and brief.
        """;

    /// <summary>Replaces the known placeholders; anything else in braces is left as written.</summary>
    public static string Fill(string template, string ministry, string portal) =>
        template.Replace("{ministry}", ministry, StringComparison.Ordinal)
                .Replace("{portal}", portal, StringComparison.Ordinal);
}
