using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Dsp.BuildingBlocks.Web;

/// <summary>Reads or creates X-Correlation-Id and adds it to the response and the logging scope.</summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string Header = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        var id = context.Request.Headers[Header].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(id)) id = Guid.NewGuid().ToString("N");

        context.Request.Headers[Header] = id;
        context.Response.Headers[Header] = id;
        context.TraceIdentifier = id;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id }))
        {
            await next(context);
        }
    }
}

/// <summary>Forwards the current correlation ID on outgoing service-to-service calls.</summary>
public sealed class CorrelationIdHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var id = accessor.HttpContext?.Request.Headers[CorrelationIdMiddleware.Header].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(id) && !request.Headers.Contains(CorrelationIdMiddleware.Header))
            request.Headers.Add(CorrelationIdMiddleware.Header, id);
        return base.SendAsync(request, ct);
    }
}
