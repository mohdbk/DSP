using Dsp.AiOrchestrator.Application;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dsp.UnitTests.AiOrchestrator;

public class ApplicationReviewAgentTests
{
    private static readonly MultilingualRenderer Renderer =
        MultilingualRenderer.CreateAsync(TestText.Localization, null, default).GetAwaiter().GetResult();

    private static ApplicationReviewAgent Agent(FakeLlm llm, string? reviewPrompt = null) =>
        new(llm, new FakeSettings(llm.Configured, reviewPrompt), TestText.Localization, NullLogger<ApplicationReviewAgent>.Instance);

    private static ApplicationReviewRequest Request(string tradeNameAr, IReadOnlyList<string>? similar = null) => new(
        new ReviewServiceInfo("TRADE_NAME_RESERVATION", TestText.Text("Trade Name Reservation", "حجز اسم تجاري"), "Reserve a name.",
            [TestText.Text("The trade name does not imitate a well-known brand.")],
            [
                new ReviewField("tradeNameEn", TestText.Text("Trade name (English)", "الاسم التجاري (بالإنجليزية)"), "text", true, null, null),
                new ReviewField("tradeNameAr", TestText.Text("Trade name (Arabic)", "الاسم التجاري (بالعربية)"), "text", true, null, null),
                new ReviewField("agreementFile", TestText.Text("Signed agreement", "الاتفاقية"), "file", false, null, null),
            ]),
        new Dictionary<string, string?> { ["tradeNameEn"] = "Falcon Trading", ["tradeNameAr"] = tradeNameAr },
        similar, 1);

    [Fact]
    public void Rules_ask_the_applicant_to_write_the_arabic_name_in_arabic()
    {
        var result = RuleBasedReview.Review(Request("Al Saqr"), Renderer);

        Assert.Equal(ReviewRecommendations.RequestInformation, result.Recommendation);
        var question = Assert.Single(result.Questions);
        Assert.Contains("Arabic letters", question["en"]);
        Assert.Contains("بالحروف العربية", question["ar"]);
        Assert.Contains(result.Findings, f => f.Severity == "issue" && f.Field == "tradeNameAr");
        Assert.Equal("rules", result.Mode);
    }

    [Fact]
    public void A_clean_application_is_recommended_with_the_checklist_for_the_officer()
    {
        var result = RuleBasedReview.Review(Request("الصقر للتجارة", ["Falcon Tradings"]), Renderer);

        Assert.Equal(ReviewRecommendations.Approve, result.Recommendation);
        Assert.Empty(result.Questions);
        Assert.Contains(result.Findings, f => f.Severity == "warning" && f.Message["en"].Contains("Falcon Tradings"));
        Assert.Contains(result.Findings, f => f.Message["en"].StartsWith("Officer to confirm:"));
    }

    [Fact]
    public async Task Without_a_model_the_built_in_checks_are_used()
    {
        var agent = Agent(new FakeLlm(null));

        var result = await agent.ReviewAsync(Request("الصقر"), default);

        Assert.Equal("rules", result.Mode);
    }

    [Fact]
    public async Task The_models_review_is_used_and_the_built_in_checks_still_apply()
    {
        var llm = new FakeLlm("""
            Here is my review:
            {"recommendation":"approve","summary":"The name looks acceptable.",
             "findings":[{"severity":"info","field":"tradeNameEn","message":"No brand imitation found."}],
             "questions":[]}
            """);
        var agent = Agent(llm);

        var result = await agent.ReviewAsync(Request("Al Saqr"), default);

        Assert.Equal("llm", result.Mode);
        Assert.Equal("test-model", result.Model);
        Assert.Equal("The name looks acceptable.", result.Summary["en"]);
        // The model missed the Latin-script Arabic name; the guardrail still asks the applicant.
        Assert.Equal(ReviewRecommendations.RequestInformation, result.Recommendation);
        Assert.Contains(result.Questions, q => q["en"].Contains("Arabic letters"));
        Assert.Empty(llm.LastTools!); // no tools are sent for a review
    }

    [Fact]
    public async Task An_unreadable_model_answer_falls_back_to_the_built_in_checks()
    {
        var agent = Agent(new FakeLlm("I think it is fine."));

        var result = await agent.ReviewAsync(Request("الصقر"), default);

        Assert.Equal("rules", result.Mode);
    }

    [Fact]
    public async Task The_review_prompt_is_editable_and_names_come_from_the_texts_while_the_fixed_rules_stay()
    {
        var llm = new FakeLlm("""{"recommendation":"approve","summary":{"en":"Fine.","ar":"سليم."},"findings":[],"questions":[]}""");

        await Agent(llm, "You review applications for {ministry} ({portal}). Be strict.").ReviewAsync(Request("الصقر"), default);

        var system = llm.LastMessages![0].Content!;
        Assert.StartsWith("You review applications for Ministry of Economy", system);
        Assert.DoesNotContain("{ministry}", system);
        Assert.Contains("Be strict.", system);
        Assert.Contains("You never decide: the officer does.", system);
        Assert.Contains("Answer with JSON only", system);
    }

    [Fact]
    public async Task Without_a_saved_prompt_the_default_is_used()
    {
        var llm = new FakeLlm("""{"recommendation":"approve","summary":{"en":"Fine.","ar":"سليم."},"findings":[],"questions":[]}""");

        await Agent(llm).ReviewAsync(Request("الصقر"), default);

        Assert.StartsWith("You are the pre-review assistant of Ministry of Economy", llm.LastMessages![0].Content!);
    }

    private sealed class FakeLlm(string? reply) : ILlmClient
    {
        public bool Configured => reply is not null;
        public IReadOnlyList<LlmTool>? LastTools { get; private set; }
        public IReadOnlyList<LlmMessage>? LastMessages { get; private set; }

        public Task<LlmReply> CompleteAsync(AiSettings settings, IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmTool> tools,
            CancellationToken ct)
        {
            LastTools = tools;
            LastMessages = messages;
            return Task.FromResult(new LlmReply(reply, []));
        }
    }

    private sealed class FakeSettings(bool configured, string? reviewPrompt = null) : IAiSettingsProvider
    {
        public Task<AiSettings> GetAsync(CancellationToken ct) => Task.FromResult(new AiSettings("test",
            configured ? "https://llm.test/v1" : "", configured ? "test-model" : "", null, 0.2, 30, false, 6, 30, null, null,
            ReviewPrompt: reviewPrompt));
    }
}
