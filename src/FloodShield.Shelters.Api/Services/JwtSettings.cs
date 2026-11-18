using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace FloodShield.Shelters.Api.Services;

public class JwtSettings
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int ExpiresMinutes { get; set; } = 60;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(Key));
}

public class RateLimitSettings
{
    public int PermitLimit { get; set; } = 60;
    public int AuthPermitLimit { get; set; } = 5;
    public int WindowSeconds { get; set; } = 60;
}
