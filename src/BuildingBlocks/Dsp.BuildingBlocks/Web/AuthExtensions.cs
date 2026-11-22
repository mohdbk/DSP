using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Dsp.BuildingBlocks.Web;

public sealed class JwtOptions
{
    public string Issuer { get; set; } = "dsp-identity";
    public string Audience { get; set; } = "dsp";
    public string Key { get; set; } = "";
    public int LifetimeMinutes { get; set; } = 240;

    public SymmetricSecurityKey SigningKey()
    {
        if (Encoding.UTF8.GetByteCount(Key) < 32)
            throw new InvalidOperationException("Jwt:Key must be at least 32 bytes.");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key));
    }
}

public static class DspClaims
{
    public const string Role = "role";
    public const string Name = "name";
    public const string EmiratesId = "emirates_id";
    public const string Email = "email";
    public const string Mobile = "mobile";
    public const string StaffRole = "staff";
    public const string CitizenRole = "citizen";
}

public static class AuthExtensions
{
    /// <summary>Validates platform tokens issued by the Identity service (signed with the shared Jwt:Key).</summary>
    public static IServiceCollection AddDspAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = new JwtOptions();
        configuration.GetSection("Jwt").Bind(jwt);
        services.AddSingleton(jwt);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.SigningKey(),
                    NameClaimType = DspClaims.Name,
                    RoleClaimType = DspClaims.Role,
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("staff", p => p.RequireRole(DspClaims.StaffRole));

        return services;
    }

    public static string UserId(this ClaimsPrincipal user) =>
        user.FindFirstValue("sub") ?? throw new InvalidOperationException("Token has no subject.");

    public static string DisplayName(this ClaimsPrincipal user) => user.FindFirstValue(DspClaims.Name) ?? "User";

    public static bool IsStaff(this ClaimsPrincipal user) => user.IsInRole(DspClaims.StaffRole);
}
