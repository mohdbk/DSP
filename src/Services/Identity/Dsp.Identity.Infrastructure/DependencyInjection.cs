using Dsp.BuildingBlocks.Persistence;
using Dsp.BuildingBlocks.Web;
using Dsp.Identity.Application;
using Dsp.Identity.Infrastructure.Persistence;
using Dsp.Identity.Infrastructure.Providers;
using Dsp.Identity.Infrastructure.Staff;
using Dsp.Identity.Infrastructure.Tokens;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dsp.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDspDbContext<IdentityDbContext, IdentitySqlServerDbContext, IdentityPostgresDbContext>(configuration, "Identity");
        services.AddScoped<IStaffAccountRepository, StaffAccountRepository>();
        services.AddSingleton<IPasswordService, PasswordService>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();

        // Citizen providers: add Google or local credentials here later.
        var uaePass = new UaePassOptions();
        configuration.GetSection("UaePass").Bind(uaePass);
        services.AddSingleton(uaePass);
        services.AddHttpClient<UaePassIdentityProvider>(c => c.Timeout = TimeSpan.FromSeconds(20));
        services.AddScoped<ICitizenIdentityProvider>(sp => sp.GetRequiredService<UaePassIdentityProvider>());

        // Staff authenticator: "local" today; an AD implementation is selected here by Staff:Authenticator later.
        var staffAuthenticator = configuration["Staff:Authenticator"] ?? "local";
        switch (staffAuthenticator)
        {
            case "local":
                services.AddScoped<IStaffAuthenticator, LocalStaffAuthenticator>();
                break;
            default:
                throw new InvalidOperationException($"Unknown staff authenticator '{staffAuthenticator}'.");
        }

        return services;
    }
}
