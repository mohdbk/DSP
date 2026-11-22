using Dsp.EServices.Domain.Rules;
using Microsoft.Extensions.DependencyInjection;

namespace Dsp.EServices.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddEServicesApplication(this IServiceCollection services)
    {
        services.AddScoped<ApplicationService>();
        services.AddScoped<ReviewService>();
        services.AddScoped<ProcessSteps>();
        services.AddScoped<StatusNotifier>();

        // Service-specific business rules (one class per service, from the extraction pipeline).
        services.AddSingleton<IServiceRules, TradeNameReservationRules>();
        services.AddSingleton<IServiceRules, CommercialAgencyRules>();
        return services;
    }
}
