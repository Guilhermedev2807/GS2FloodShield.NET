using System.IdentityModel.Tokens.Jwt;
using FloodShield.Shelters.Api.Domain;
using FloodShield.Shelters.Api.Services;
using Microsoft.Extensions.Options;

namespace FloodShield.Shelters.Tests.Unit;

public class TokenServiceTests
{
    private static TokenService NewService() => new(Options.Create(new JwtSettings
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        Key = new string('k', 48),
        ExpiresMinutes = 30
    }));

    [Fact]
    public void Create_ContainsSubjectRoleIssuerAndExpiration()
    {
        var user = new User { Name = "Ana", Email = "ana@test.local", Role = UserRole.Admin };

        var (token, expiresAt) = NewService().Create(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("test-issuer", jwt.Issuer);
        Assert.Contains("test-audience", jwt.Audiences);
        Assert.Equal(user.Id.ToString(), jwt.Subject);
        Assert.Equal("Admin", jwt.Claims.First(c => c.Type == "role").Value);
        Assert.True(expiresAt > DateTime.UtcNow.AddMinutes(29));
    }
}
