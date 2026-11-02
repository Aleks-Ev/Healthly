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
    public const string LaboratoryClaim = "lab";

    private readonly JwtOptions _options;

    public TokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public (string Token, DateTime ExpiresAt) CreateToken(User user)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_options.ExpiresMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Name, user.FullName),
            new("role", user.Role),
        };
        // Сотруднику лаборатории — её id: по нему проверяется доступ к документам (CanAccessDocument)
        if (user.Role == Roles.LabStaff && user.LaboratoryId is { } laboratoryId)
        {
            claims.Add(new Claim(LaboratoryClaim, laboratoryId.ToString()));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(_options.GetSigningKey(), SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(claims),
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

    // Лаборатория сотрудника (claim "lab", только у LabStaff)
    public static int? GetLaboratoryId(this ClaimsPrincipal principal) =>
        int.TryParse(principal.FindFirstValue(TokenService.LaboratoryClaim), out var id) ? id : null;

    // Доступ к документу: как к данным его владельца (сам, Doctor, Admin) или сотрудник лаборатории, которая его загрузила.
    // Сотрудник видит только документы своей лаборатории — не всю медкарту пациента
    public static bool CanAccessDocument(this ClaimsPrincipal principal, MedicalDocument document) =>
        principal.CanAccessUser(document.UserId)
        || (principal.IsInRole(Roles.LabStaff) && document.LaboratoryId != null && principal.GetLaboratoryId() == document.LaboratoryId);
}
