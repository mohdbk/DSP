using Dsp.AiOrchestrator.Application;
using Dsp.AiOrchestrator.Infrastructure;
using Dsp.BuildingBlocks.Localization;
using Dsp.BuildingBlocks.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddDspDefaults("dsp-ai-orchestrator");
builder.Services.AddDspAuthentication(builder.Configuration);
builder.Services.AddAiOrchestratorApplication();
builder.Services.AddAiOrchestratorInfrastructure(builder.Configuration);
builder.Services.AddDspLocalization(builder.Configuration);

var app = builder.Build();
app.UseDspDefaults();
app.UseAuthentication();
app.UseAuthorization();
app.UseDspLocalization();

// Service-to-service: the AI pre-review of a submitted application (agentic process). Not exposed by the gateway.
app.MapPost("/internal/ai/review", async (ApplicationReviewRequest request, ApplicationReviewAgent agent,
        CancellationToken ct) => await agent.ReviewAsync(request, ct))
    .RequireInternalKey().AllowAnonymous();

app.MapApiDocuments("ai");

var api = app.MapApi("ai").RequireAuthorization();

api.MapGet("/status", async (IAiSettingsProvider settings, CancellationToken ct) =>
{
    var s = await settings.GetAsync(ct);
    return new { mode = s.ChatActive ? "llm" : "guided", model = s.ChatActive ? s.Model : null, review = s.ReviewActive };
});

api.MapPost("/chat", async (ChatRequest request, ChatService chat, CancellationToken ct) =>
    await chat.ChatAsync(request, ct));

app.Run();
