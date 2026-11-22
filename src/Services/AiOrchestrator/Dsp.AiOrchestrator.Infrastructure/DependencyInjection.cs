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
        services.AddHttpClient<ILlmClient, OpenAiCompatibleLlmClient>(c => c.Timeout = TimeSpan.FromSeconds(90));

        services.AddTransient<BearerForwardingHandler>();
        services.AddHttpClient<IEServicesApi, EServicesApiClient>(c =>
                c.BaseAddress = new Uri(configuration["Services:EServices"] ?? "http://eservices.railway.internal:8080"))
            .AddHttpMessageHandler<BearerForwardingHandler>()
            .AddHttpMessageHandler<CorrelationIdHandler>();
        return services;
    }
}
