using System.IdentityModel.Tokens.Jwt;
using Dsp.BuildingBlocks.Web;
using Dsp.Identity.Domain;
using Dsp.Identity.Infrastructure.Tokens;
using Microsoft.IdentityModel.Tokens;

namespace Dsp.UnitTests.Identity;

public class JwtTokenIssuerTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "dsp-identity",
        Audience = "dsp",
        Key = "unit-test-signing-key-that-is-long-enough-1234567890",
        LifetimeMinutes = 60,
    };

    [Fact]
    public void Citizen_token_carries_profile_claims_and_validates()
    {
        var user = new ExternalProfile("uaepass", "uuid-1", "Fatima", EmiratesId: "784-1990-1234567-1",
            Email: "f@example.ae", AccountLevel: "SOP3").ToCitizen();

        var issued = new JwtTokenIssuer(Options).Issue(user);

        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(issued.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = Options.SigningKey(),
        }, out _);

        Assert.Equal("uaepass:uuid-1", principal.FindFirst("sub")?.Value);
        Assert.Equal(DspClaims.CitizenRole, principal.FindFirst(DspClaims.Role)?.Value);
        Assert.Equal("784-1990-1234567-1", principal.FindFirst(DspClaims.EmiratesId)?.Value);
        Assert.Equal("SOP3", principal.FindFirst("account_level")?.Value);
        Assert.True(issued.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(59));
    }

    [Fact]
    public void Token_signed_with_another_key_is_rejected()
    {
        var token = new JwtTokenIssuer(Options).Issue(
            new ExternalProfile("uaepass", "u", "X").ToCitizen()).AccessToken;

        var other = new JwtOptions { Key = "a-completely-different-signing-key-0987654321-abcdef" };
        Assert.ThrowsAny<SecurityTokenException>(() => new JwtSecurityTokenHandler().ValidateToken(token,
            new TokenValidationParameters
            {
                ValidIssuer = Options.Issuer,
                ValidAudience = Options.Audience,
                IssuerSigningKey = other.SigningKey(),
            }, out _));
    }

    [Fact]
    public void Short_signing_keys_are_refused()
    {
        Assert.Throws<InvalidOperationException>(() => new JwtOptions { Key = "too-short" }.SigningKey());
    }
}
