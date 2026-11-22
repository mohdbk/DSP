using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Dsp.BuildingBlocks.Web;

/// <summary>
/// API conventions shared by every service (see AGENTS.md):
/// public endpoints live under <c>/api/v{n}/{service}</c>, each version has an OpenAPI document at
/// <c>/api/v{n}/{service}/openapi.json</c>, errors are RFC 9457 problem details and lists are <c>PagedList</c>.
/// A breaking change means a new version next to the old one, never an edit of the old one.
/// </summary>
public static class ApiVersions
{
    public const string V1 = "v1";
    public const string Current = V1;
    public const string Header = "api-version";

    /// <summary>The route group for one version of a service's public API, e.g. <c>/api/v1/eservices</c>.</summary>
    public static RouteGroupBuilder MapApi(this IEndpointRouteBuilder app, string service, string version = Current) =>
        app.MapGroup($"/api/{version}/{service}")
            .WithTags(service)
            .AddEndpointFilter(async (context, next) =>
            {
                context.HttpContext.Response.Headers[Header] = version;
                return await next(context);
            });

    /// <summary>Publishes the service's OpenAPI documents at <c>/api/{version}/{service}/openapi.json</c> (anonymous).</summary>
    public static IEndpointConventionBuilder MapApiDocuments(this WebApplication app, string service) =>
        app.MapOpenApi($"/api/{{documentName}}/{service}/openapi.json").AllowAnonymous();

    internal static void AddApiDocuments(this IServiceCollection services, string serviceName) =>
        services.AddOpenApi(Current, options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = $"{serviceName} API";
            document.Info.Version = Current;
            document.Info.Description = "Digital Services Platform. Errors are RFC 9457 problem details; lists are { items, page, pageSize, total }.";
            return Task.CompletedTask;
        }));
}
