using HealthlyApi.Models;
using Microsoft.AspNetCore.Identity;

namespace HealthlyApi.Services;

// Обёртка над PasswordHasher<User> (PBKDF2) без подключения полного ASP.NET Identity
public class PasswordService
{
    private readonly PasswordHasher<User> _hasher = new();

    // Хэш случайного пароля — для проверки «вхолостую», когда пользователя с таким e-mail нет
    private static readonly Lazy<string> DummyHash = new(() =>
        new PasswordHasher<User>().HashPassword(new User(), Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32))));

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);

    // Проверка пароля, которая всегда занимает столько же времени, сколько настоящая (PBKDF2), и всегда false.
    // Без неё «нет такого e-mail» отвечал бы за 1 мс, а «неверный пароль» — за ~50 мс: по времени ответа
    // можно было бы перебором узнать, какие адреса зарегистрированы (timing attack)
    public void VerifyDummy(string password) =>
        _hasher.VerifyHashedPassword(new User(), DummyHash.Value, password);

    public bool Verify(User user, string password)
    {
        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return false;
        }

        try
        {
            return _hasher.VerifyHashedPassword(user, user.PasswordHash, password)
                != PasswordVerificationResult.Failed;
        }
        catch (FormatException)
        {
            // В PasswordHash лежит не хэш (например, значение, вставленное вручную через pgAdmin)
            return false;
        }
    }
}
