using System.Security.Claims;
using System.Text;
using HealthlyApi.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HealthlyApi.Services;

// Секция "Jwt" из конфигурации (Key — в user-secrets)
public class JwtOptions
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int ExpiresMinutes { get; set; } = 15;      // access-токен (JWT)
    public int RefreshTokenDays { get; set; } = 30;    // refresh-токен; при каждом обновлении срок отсчитывается заново

    public SymmetricSecurityKey GetSigningKey() => new(Encoding.UTF8.GetBytes(Key));
}

public class TokenService
{
    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public (string Token, DateTime ExpiresAt) CreateToken(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.ExpiresMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(_options.GetSigningKey(), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.FullName),
                new Claim("role", user.Role),
            ]),
        };

        return (new JsonWebTokenHandler().CreateToken(descriptor), expiresAt);
    }
}

public static class ClaimsPrincipalExtensions
{
    // Id текущего пользователя из claim "sub" (MapInboundClaims отключён в Program.cs)
    public static int? GetUserId(this ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub), out var id) ? id : null;

    public static bool IsStaff(this ClaimsPrincipal principal) =>
        principal.IsInRole(Roles.Doctor) || principal.IsInRole(Roles.Admin);

    // Доступ к данным пользователя: сам пользователь, Doctor или Admin
    public static bool CanAccessUser(this ClaimsPrincipal principal, int userId) =>
        principal.GetUserId() == userId || principal.IsStaff();
}
