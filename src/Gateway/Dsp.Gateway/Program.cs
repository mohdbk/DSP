using System.Threading.RateLimiting;
using Dsp.BuildingBlocks.Web;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;

var builder = WebApplication.CreateBuilder(args);
builder.AddDspDefaults("dsp-gateway");

// One route per service and API version: /api/v1/{service}/** (see ApiVersions in the building blocks).
// The unversioned paths of the first release (/api/{service}/**) still work: they are forwarded to the current
// version with Deprecation, Sunset and successor Link headers, so existing clients and the UAE PASS redirect URI
// keep working while callers move to /api/v1. /internal/** is never routed.
const string currentVersion = "v1";
const string legacySunset = "Wed, 30 Jun 2027 00:00:00 GMT";

var services = new Dictionary<string, (string DefaultUrl, TimeSpan Timeout)>
{
    ["identity"] = ("http://identity.railway.internal:8080", TimeSpan.FromSeconds(30)),
    ["eservices"] = ("http://eservices.railway.internal:8080", TimeSpan.FromSeconds(60)),
    ["payments"] = ("http://payments.railway.internal:8080", TimeSpan.FromSeconds(30)),
    ["documents"] = ("http://documents.railway.internal:8080", TimeSpan.FromSeconds(60)),
    ["notifications"] = ("http://notifications.railway.internal:8080", TimeSpan.FromSeconds(30)),
    ["ai"] = ("http://ai-orchestrator.railway.internal:8080", TimeSpan.FromSeconds(150)),
};

var routes = services.SelectMany(s => new[]
{
    new RouteConfig
    {
        RouteId = $"{s.Key}-{currentVersion}",
        ClusterId = s.Key,
        RateLimiterPolicy = "per-client",
        Match = new RouteMatch { Path = $"/api/{currentVersion}/{s.Key}/{{**rest}}" },
    },
    new RouteConfig
    {
        RouteId = $"{s.Key}-legacy",
        ClusterId = s.Key,
        RateLimiterPolicy = "per-client",
        Match = new RouteMatch { Path = $"/api/{s.Key}/{{**rest}}" },
        Transforms =
        [
            new Dictionary<string, string> { ["PathPattern"] = $"/api/{currentVersion}/{s.Key}/{{**rest}}" },
            new Dictionary<string, string> { ["ResponseHeader"] = "Deprecation", ["Set"] = "true", ["When"] = "Always" },
            new Dictionary<string, string> { ["ResponseHeader"] = "Sunset", ["Set"] = legacySunset, ["When"] = "Always" },
            new Dictionary<string, string>
            {
                ["ResponseHeader"] = "Link", ["Set"] = $"</api/{currentVersion}/{s.Key}>; rel=\"successor-version\"", ["When"] = "Always",
            },
        ],
    },
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

builder.Services.AddReverseProxy()
    .LoadFromMemory(routes, clusters)
    // Recycle connections so a redeployed service (new private IP) is picked up quickly.
    .ConfigureHttpClient((_, handler) => handler.PooledConnectionLifetime = TimeSpan.FromMinutes(1));

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
app.MapGet("/", () => Results.Ok(new { name = "DSP API Gateway", status = "ok", apiVersion = currentVersion }));
app.MapReverseProxy();
app.Run();
