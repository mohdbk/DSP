using System.Threading.RateLimiting;
using Dsp.BuildingBlocks.Web;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

var builder = WebApplication.CreateBuilder(args);
builder.AddDspDefaults("dsp-gateway");

// One route per service. Only /api/{service}/** is exposed; /internal/** is never routed.
var services = new Dictionary<string, (string Prefix, string DefaultUrl, TimeSpan Timeout)>
{
    ["identity"] = ("/api/identity", "http://identity.railway.internal:8080", TimeSpan.FromSeconds(30)),
    ["eservices"] = ("/api/eservices", "http://eservices.railway.internal:8080", TimeSpan.FromSeconds(60)),
    ["payments"] = ("/api/payments", "http://payments.railway.internal:8080", TimeSpan.FromSeconds(30)),
    ["documents"] = ("/api/documents", "http://documents.railway.internal:8080", TimeSpan.FromSeconds(60)),
    ["notifications"] = ("/api/notifications", "http://notifications.railway.internal:8080", TimeSpan.FromSeconds(30)),
    ["ai"] = ("/api/ai", "http://ai-orchestrator.railway.internal:8080", TimeSpan.FromSeconds(150)),
};

var routes = services.Select(s => new RouteConfig
{
    RouteId = s.Key,
    ClusterId = s.Key,
    RateLimiterPolicy = "per-client",
    Match = new RouteMatch { Path = $"{s.Value.Prefix}/{{**rest}}" },
}).ToList();

var clusters = services.Select(s => new ClusterConfig
{
    ClusterId = s.Key,
    HttpRequest = new ForwarderRequestConfig { ActivityTimeout = s.Value.Timeout },
    Destinations = new Dictionary<string, DestinationConfig>
    {
        ["primary"] = new() { Address = builder.Configuration[$"Services:{s.Key}"] ?? s.Value.DefaultUrl },
    },
}).ToList();

builder.Services.AddReverseProxy().LoadFromMemory(routes, clusters);

// Basic abuse protection: 300 requests per minute per client IP.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("per-client", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();
app.UseDspDefaults();
app.UseRateLimiter();
app.MapGet("/", () => Results.Ok(new { name = "DSP API Gateway", status = "ok" }));
app.MapReverseProxy();
app.Run();
