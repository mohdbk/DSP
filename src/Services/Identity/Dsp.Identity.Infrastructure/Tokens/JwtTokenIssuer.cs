using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dsp.BuildingBlocks.Web;
using Dsp.Identity.Application;
using Dsp.Identity.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Dsp.Identity.Infrastructure.Tokens;

/// <summary>Issues the platform access token used by every DSP service (validated with the shared Jwt:Key).</summary>
public sealed class JwtTokenIssuer(JwtOptions options) : ITokenIssuer
{
    public IssuedToken Issue(PlatformUser user)
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(options.LifetimeMinutes);
        var claims = new List<Claim>
        {
            new("sub", user.Subject),
            new(DspClaims.Name, user.FullName),
            new(DspClaims.Role, user.Role == PlatformRole.Staff ? DspClaims.StaffRole : DspClaims.CitizenRole),
            new("idp", user.Provider),
            new("jti", Guid.NewGuid().ToString("N")),
        };
        if (user.FullNameAr is not null) claims.Add(new("name_ar", user.FullNameAr));
        if (user.EmiratesId is not null) claims.Add(new(DspClaims.EmiratesId, user.EmiratesId));
        if (user.Email is not null) claims.Add(new(DspClaims.Email, user.Email));
        if (user.Mobile is not null) claims.Add(new(DspClaims.Mobile, user.Mobile));
        if (user.AccountLevel is not null) claims.Add(new("account_level", user.AccountLevel));
        if (user.Title is not null) claims.Add(new("title", user.Title));

        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(options.SigningKey(), SecurityAlgorithms.HmacSha256));

        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
