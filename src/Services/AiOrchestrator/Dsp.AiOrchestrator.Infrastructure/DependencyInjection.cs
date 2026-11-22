using Dsp.AiOrchestrator.Application;
using Dsp.BuildingBlocks.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dsp.AiOrchestrator.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAiOrchestratorInfrastructure(this IServiceCollection services,
        IConfiguration configuration)
    {
        var llm = new LlmOptions();
        configuration.GetSection("Llm").Bind(llm);
        services.AddSingleton(llm);
        // Each call is bounded by the timeout in the AI settings; this is only an outer limit.
        services.AddHttpClient<ILlmClient, OpenAiCompatibleLlmClient>(c => c.Timeout = TimeSpan.FromMinutes(11));
        services.AddHttpClient<IAiSettingsProvider, PlatformAiSettingsProvider>(c =>
        {
            c.BaseAddress = new Uri(configuration["Services:Platform"] ?? "http://platform.railway.internal:8080");
            c.DefaultRequestHeaders.Add(InternalApi.Header, configuration["Internal:ApiKey"] ?? "");
            c.Timeout = TimeSpan.FromSeconds(10);
        });

        services.AddTransient<BearerForwardingHandler>();
        services.AddHttpClient<IEServicesApi, EServicesApiClient>(c =>
                c.BaseAddress = new Uri(configuration["Services:EServices"] ?? "http://eservices.railway.internal:8080"))
            .AddHttpMessageHandler<BearerForwardingHandler>()
            .AddHttpMessageHandler<CorrelationIdHandler>();
        return services;
    }
}
