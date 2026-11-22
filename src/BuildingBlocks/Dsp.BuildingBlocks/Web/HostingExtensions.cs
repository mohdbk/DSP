using Dsp.SharedKernel;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Dsp.BuildingBlocks.Web;

public static class HostingExtensions
{
    /// <summary>Common service defaults: port binding, JSON logs, tracing, problem details, health.</summary>
    public static WebApplicationBuilder AddDspDefaults(this WebApplicationBuilder builder, string serviceName)
    {
        var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
        builder.WebHost.UseUrls($"http://*:{port}");

        builder.Logging.ClearProviders();
        // Single-line logs read cleanly in Railway; OpenTelemetry carries structured data to the Grafana stack.
        builder.Logging.AddSimpleConsole(o =>
        {
            o.SingleLine = true;
            o.IncludeScopes = true;
            o.TimestampFormat = "HH:mm:ss ";
            o.UseUtcTimestamp = true;
        });

        // OpenTelemetry: logs, traces and metrics, exported over OTLP (to the Grafana stack) when an endpoint is set.
        // Health probes are left out of traces so they don't drown real traffic.
        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r
                .AddService(serviceName, serviceNamespace: "dsp")
                .AddAttributes([new("deployment.environment", builder.Environment.EnvironmentName)]))
            .WithLogging()
            .WithTracing(t => t
                .AddAspNetCoreInstrumentation(o => o.Filter = ctx => ctx.Request.Path != "/health")
                .AddHttpClientInstrumentation()
                .AddSource("Elsa.Workflows"))
            .WithMetrics(m => m
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("Microsoft.AspNetCore.Hosting", "Microsoft.AspNetCore.Server.Kestrel", "System.Net.Http"));
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            otel.UseOtlpExporter();

        builder.Services.AddProblemDetails();
        builder.Services.AddApiDocuments(serviceName);
        builder.Services.AddHealthChecks();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<CorrelationIdHandler>();
        return builder;
    }

    /// <summary>Correlation IDs, exception mapping and /health.</summary>
    public static WebApplication UseDspDefaults(this WebApplication app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseExceptionHandler(errorApp => errorApp.Run(WriteProblemAsync));
        app.MapHealthChecks("/health").AllowAnonymous();
        return app;
    }

    private static async Task WriteProblemAsync(HttpContext context)
    {
        var ex = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var (status, title) = ex switch
        {
            ValidationException => (StatusCodes.Status422UnprocessableEntity, "Validation failed"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
            DomainException => (StatusCodes.Status409Conflict, "Business rule violated"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error"),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            context.RequestServices.GetRequiredService<ILoggerFactory>()
                .CreateLogger("Dsp.Errors").LogError(ex, "Unhandled exception");
        }

        context.Response.StatusCode = status;
        context.Response.Headers[CorrelationIdMiddleware.Header] = context.TraceIdentifier;
        await Results.Problem(
            title: title,
            detail: status == 500 ? "Something went wrong. Quote the correlation ID when reporting." : ex?.Message,
            statusCode: status,
            extensions: new Dictionary<string, object?>
            {
                ["correlationId"] = context.TraceIdentifier,
                ["errors"] = (ex as ValidationException)?.Errors,
            }).ExecuteAsync(context);
    }
}
