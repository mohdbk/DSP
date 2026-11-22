using Microsoft.Extensions.DependencyInjection;

namespace Dsp.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddScoped<CitizenSignInService>();
        services.AddScoped<StaffSignInService>();
        services.AddScoped<StaffAccountSeeder>();
        return services;
    }
}
