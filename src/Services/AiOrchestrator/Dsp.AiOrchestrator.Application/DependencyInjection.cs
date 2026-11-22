using Microsoft.Extensions.DependencyInjection;

namespace Dsp.AiOrchestrator.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAiOrchestratorApplication(this IServiceCollection services)
    {
        services.AddScoped<ChatService>();
        services.AddScoped<GuidedConversation>();
        services.AddScoped<A2UiScreens>();
        services.AddScoped<AgUiChatRunner>();
        services.AddScoped<ApplicationReviewAgent>();
        return services;
    }
}
