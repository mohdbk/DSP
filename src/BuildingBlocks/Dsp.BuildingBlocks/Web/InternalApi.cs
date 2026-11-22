using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dsp.BuildingBlocks.Web;

/// <summary>
/// Service-to-service endpoints under /internal. The gateway never routes /internal,
/// and callers must present the shared Internal:ApiKey in X-Internal-Key.
/// </summary>
public static class InternalApi
{
    public const string Header = "X-Internal-Key";

    public static TBuilder RequireInternalKey<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.AddEndpointFilter(async (context, next) =>
        {
            var config = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var expected = config["Internal:ApiKey"];
            var provided = context.HttpContext.Request.Headers[Header].FirstOrDefault();

            if (string.IsNullOrEmpty(expected) || provided is null ||
                !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided)))
            {
                return Results.Unauthorized();
            }

            return await next(context);
        });
        return builder;
    }
}
