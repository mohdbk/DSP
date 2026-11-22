using Dsp.AiOrchestrator.Application;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dsp.UnitTests.AiOrchestrator;

public class ApplicationReviewAgentTests
{
    private static ApplicationReviewRequest Request(string tradeNameAr, IReadOnlyList<string>? similar = null) => new(
        new ReviewServiceInfo("TRADE_NAME_RESERVATION", "Trade Name Reservation", "حجز اسم تجاري", "Reserve a name.",
            ["The trade name does not imitate a well-known brand."],
            [
                new ReviewField("tradeNameEn", "Trade name (English)", "الاسم التجاري (بالإنجليزية)", "text", true, null, null),
                new ReviewField("tradeNameAr", "Trade name (Arabic)", "الاسم التجاري (بالعربية)", "text", true, null, null),
                new ReviewField("agreementFile", "Signed agreement", "الاتفاقية", "file", false, null, null),
            ]),
        new Dictionary<string, string?> { ["tradeNameEn"] = "Falcon Trading", ["tradeNameAr"] = tradeNameAr },
        similar, 1);

    [Fact]
    public void Rules_ask_the_applicant_to_write_the_arabic_name_in_arabic()
    {
        var result = RuleBasedReview.Review(Request("Al Saqr"));

        Assert.Equal(ReviewRecommendations.RequestInformation, result.Recommendation);
        var question = Assert.Single(result.Questions);
        Assert.Contains("Arabic letters", question.En);
        Assert.Contains("بالحروف العربية", question.Ar);
        Assert.Contains(result.Findings, f => f.Severity == "issue" && f.Field == "tradeNameAr");
        Assert.Equal("rules", result.Mode);
    }

    [Fact]
    public void A_clean_application_is_recommended_with_the_checklist_for_the_officer()
    {
        var result = RuleBasedReview.Review(Request("الصقر للتجارة", ["Falcon Tradings"]));

        Assert.Equal(ReviewRecommendations.Approve, result.Recommendation);
        Assert.Empty(result.Questions);
        Assert.Contains(result.Findings, f => f.Severity == "warning" && f.Message.Contains("Falcon Tradings"));
        Assert.Contains(result.Findings, f => f.Message.StartsWith("Officer to confirm:"));
    }

    [Fact]
    public async Task Without_a_model_the_built_in_checks_are_used()
    {
        var agent = new ApplicationReviewAgent(new FakeLlm(null), NullLogger<ApplicationReviewAgent>.Instance);

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
        var agent = new ApplicationReviewAgent(llm, NullLogger<ApplicationReviewAgent>.Instance);

        var result = await agent.ReviewAsync(Request("Al Saqr"), default);

        Assert.Equal("llm", result.Mode);
        Assert.Equal("test-model", result.Model);
        Assert.Equal("The name looks acceptable.", result.Summary);
        // The model missed the Latin-script Arabic name; the guardrail still asks the applicant.
        Assert.Equal(ReviewRecommendations.RequestInformation, result.Recommendation);
        Assert.Contains(result.Questions, q => q.En.Contains("Arabic letters"));
        Assert.Empty(llm.LastTools!); // no tools are sent for a review
    }

    [Fact]
    public async Task An_unreadable_model_answer_falls_back_to_the_built_in_checks()
    {
        var agent = new ApplicationReviewAgent(new FakeLlm("I think it is fine."), NullLogger<ApplicationReviewAgent>.Instance);

        var result = await agent.ReviewAsync(Request("الصقر"), default);

        Assert.Equal("rules", result.Mode);
    }

    private sealed class FakeLlm(string? reply) : ILlmClient
    {
        public bool IsConfigured => reply is not null;
        public string? Model => "test-model";
        public IReadOnlyList<LlmTool>? LastTools { get; private set; }

        public Task<LlmReply> CompleteAsync(IReadOnlyList<LlmMessage> messages, IReadOnlyList<LlmTool> tools, CancellationToken ct)
        {
            LastTools = tools;
            return Task.FromResult(new LlmReply(reply, []));
        }
    }
}
