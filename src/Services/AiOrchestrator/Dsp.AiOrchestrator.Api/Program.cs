using Dsp.AiOrchestrator.Application;
using Dsp.AiOrchestrator.Infrastructure;
using Dsp.BuildingBlocks.Web;

var builder = WebApplication.CreateBuilder(args);
builder.AddDspDefaults("dsp-ai-orchestrator");
builder.Services.AddDspAuthentication(builder.Configuration);
builder.Services.AddAiOrchestratorApplication();
builder.Services.AddAiOrchestratorInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseDspDefaults();
app.UseAuthentication();
app.UseAuthorization();

// Service-to-service: the AI pre-review of a submitted application (agentic process). Not exposed by the gateway.
app.MapPost("/internal/ai/review", async (ApplicationReviewRequest request, ApplicationReviewAgent agent,
        CancellationToken ct) => await agent.ReviewAsync(request, ct))
    .RequireInternalKey().AllowAnonymous();

app.MapApiDocuments("ai");

var api = app.MapApi("ai").RequireAuthorization();

api.MapGet("/status", (ILlmClient llm) => new
{
    mode = llm.IsConfigured ? "llm" : "guided",
    model = llm.IsConfigured ? llm.Model : null,
});

api.MapPost("/chat", async (ChatRequest request, ChatService chat, CancellationToken ct) =>
    await chat.ChatAsync(request, ct));

app.Run();
