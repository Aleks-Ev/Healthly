using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using HealthlyApi.Data;
using HealthlyApi.Models;

namespace HealthlyApi.Services;

// Выдача, ротация и отзыв refresh-токенов.
//
// Схема:
// 1. Вход/регистрация → клиент получает access-токен (JWT, живёт Jwt:ExpiresMinutes) и refresh-токен (Jwt:RefreshTokenDays).
// 2. Access-токен истёк (401) → POST /api/auth/refresh { refreshToken } → новая пара; старый refresh-токен отозван (ротация).
// 3. Повторное использование уже обменянного токена = признак кражи → отзываются ВСЕ сессии пользователя.
//    Исключение — «гонка»: тот же токен пришёл в течение ReuseGracePeriod после обмена (две вкладки/запроса одновременно);
//    тогда просто 401 без отзыва остальных сессий, клиент возьмёт уже сохранённую новую пару.
public class RefreshTokenService
{
    private static readonly TimeSpan ReuseGracePeriod = TimeSpan.FromSeconds(30);

    private readonly AppDbContext _context;
    private readonly JwtOptions _options;
    private readonly ILogger<RefreshTokenService> _logger;

    public RefreshTokenService(AppDbContext context, IOptions<JwtOptions> options, ILogger<RefreshTokenService> logger)
    {
        _context = context;
        _options = options.Value;
        _logger = logger;
    }

    // Результат обмена: User == null — токен недействителен (ответ 401)
    public record RotationResult(User? User, string? Token, DateTime ExpiresAt);

    // Новый refresh-токен для пользователя (пользователь уже должен быть сохранён в БД — нужен его Id)
    public async Task<(string Token, DateTime ExpiresAt)> IssueAsync(User user)
    {
        var (token, hash) = Generate();
        var expiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays);

        _context.RefreshTokens.Add(new RefreshToken { UserId = user.Id, TokenHash = hash, ExpiresAt = expiresAt });
        await RemoveExpiredAsync(user.Id);
        await _context.SaveChangesAsync();

        return (token, expiresAt);
    }

    public async Task<RotationResult> RotateAsync(string token)
    {
        var now = DateTime.UtcNow;
        var stored = await _context.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == Hash(token));

        if (stored?.User == null || stored.ExpiresAt <= now)
        {
            return Invalid();
        }

        if (stored.RevokedAt != null)
        {
            // Признак кражи — только повтор токена, который уже был ОБМЕНЯН (есть ReplacedByTokenHash), и не в «гонке».
            // Токен, отозванный выходом или сменой пароля, просто недействителен: остальные сессии не трогаем
            var wasRotated = stored.ReplacedByTokenHash != null;
            var isRace = now - stored.RevokedAt < ReuseGracePeriod;
            if (wasRotated && !isRace)
            {
                _logger.LogWarning("Refresh token reuse detected for user {UserId}; revoking all sessions", stored.UserId);
                await RevokeAllAsync(stored.UserId);
            }
            return Invalid();
        }

        var (newToken, newHash) = Generate();
        var expiresAt = now.AddDays(_options.RefreshTokenDays);

        // Атомарно: отзываем, только если токен всё ещё активен. Если параллельный запрос успел первым — 0 строк → 401
        var revoked = await _context.RefreshTokens
            .Where(t => t.Id == stored.Id && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.ReplacedByTokenHash, newHash));

        if (revoked == 0)
        {
            return Invalid();
        }

        _context.RefreshTokens.Add(new RefreshToken { UserId = stored.UserId, TokenHash = newHash, ExpiresAt = expiresAt });
        await _context.SaveChangesAsync();

        return new RotationResult(stored.User, newToken, expiresAt);
    }

    // Выход: отзываем один токен (неизвестный или уже отозванный — не ошибка)
    public Task RevokeAsync(string token) =>
        _context.RefreshTokens
            .Where(t => t.TokenHash == Hash(token) && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, DateTime.UtcNow));

    // Все сессии пользователя (смена пароля, подозрение на кражу токена)
    public Task RevokeAllAsync(int userId) =>
        _context.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAt, DateTime.UtcNow));

    // Истёкшие токены больше не нужны даже для обнаружения повторного использования
    private Task RemoveExpiredAsync(int userId) =>
        _context.RefreshTokens
            .Where(t => t.UserId == userId && t.ExpiresAt <= DateTime.UtcNow)
            .ExecuteDeleteAsync();

    private static RotationResult Invalid() => new(null, null, default);

    // 32 случайных байта (256 бит) в base64url — угадать невозможно, в URL/JSON передаётся без экранирования
    private static (string Token, string Hash) Generate()
    {
        var token = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        return (token, Hash(token));
    }

    // SHA-256 в hex (64 символа). Соль не нужна: токен случайный и длинный, перебором его не подобрать
    private static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
