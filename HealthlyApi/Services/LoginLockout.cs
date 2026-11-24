using HealthlyApi.Models;

namespace HealthlyApi.Services;

// Временная блокировка аккаунта после серии неверных паролей — защита от подбора пароля (brute force).
// Лимит RateLimiting.Auth считает попытки с одного IP; злоумышленник с многих IP (ботнет, прокси) его обходит,
// а этот счётчик привязан к самому аккаунту:
//   - MaxFailedAttempts неверных паролей подряд → вход закрыт на LockoutDuration (даже с правильным паролем);
//   - успешный вход, смена пароля или новый пароль от администратора обнуляют счётчик;
//   - после окончания блокировки счёт начинается заново.
// Ответ при блокировке (429) выдаёт, что аккаунт существует, — это не новая утечка: регистрация и так отвечает
// 409 «email уже занят». Чистые функции (без БД и часов) — проверяются тестами LoginLockoutTests.
public static class LoginLockout
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // Сколько ещё ждать или null, если вход открыт
    public static TimeSpan? RemainingLockout(User user, DateTime nowUtc) =>
        user.LockoutEndsAt is { } end && end > nowUtc ? end - nowUtc : null;

    // Неверный пароль. true — этой попыткой аккаунт заблокирован
    public static bool RegisterFailure(User user, DateTime nowUtc)
    {
        if (user.LockoutEndsAt is { } end && end <= nowUtc)
        {
            // Прошлая блокировка истекла — считаем с нуля
            user.LockoutEndsAt = null;
            user.FailedLoginCount = 0;
        }

        user.FailedLoginCount++;
        if (user.FailedLoginCount < MaxFailedAttempts)
        {
            return false;
        }
        user.LockoutEndsAt = nowUtc + LockoutDuration;
        user.FailedLoginCount = 0;
        return true;
    }

    // Успешный вход или новый пароль. true — что-то поменялось (нужно сохранить)
    public static bool Reset(User user)
    {
        if (user.FailedLoginCount == 0 && user.LockoutEndsAt == null)
        {
            return false;
        }
        user.FailedLoginCount = 0;
        user.LockoutEndsAt = null;
        return true;
    }

    // Текст для пользователя: «Try again in 15 minutes»
    public static string Message(TimeSpan remaining)
    {
        var minutes = Math.Max(1, (int)Math.Ceiling(remaining.TotalMinutes));
        return $"Too many failed sign-in attempts. For your security the account is locked — try again in {minutes} minute{(minutes == 1 ? "" : "s")}.";
    }
}
