namespace HealthlyApi.Models;

// Refresh-токен: долгоживущий ключ для получения новой пары токенов без пароля (POST /api/auth/refresh).
// Сам токен клиенту отдаётся один раз, в БД хранится только его SHA-256-хэш — утечка таблицы не даёт войти.
// Ротация: при каждом обновлении старый токен отзывается (RevokedAt) и ссылается на новый (ReplacedByTokenHash).
public class RefreshToken
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    // null — токен активен; иначе — когда отозван (обновлён, выход, смена пароля)
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByTokenHash { get; set; }
}
