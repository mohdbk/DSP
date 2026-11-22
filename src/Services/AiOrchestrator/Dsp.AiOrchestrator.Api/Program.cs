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

var api = app.MapGroup("/api/ai").RequireAuthorization();

api.MapGet("/status", (ILlmClient llm) => new
{
    mode = llm.IsConfigured ? "llm" : "guided",
    model = llm.IsConfigured ? llm.Model : null,
});

api.MapPost("/chat", async (ChatRequest request, ChatService chat, CancellationToken ct) =>
    await chat.ChatAsync(request, ct));

app.Run();
