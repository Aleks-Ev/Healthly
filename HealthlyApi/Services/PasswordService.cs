using HealthlyApi.Models;
using Microsoft.AspNetCore.Identity;

namespace HealthlyApi.Services;

// Обёртка над PasswordHasher<User> (PBKDF2) без подключения полного ASP.NET Identity
public class PasswordService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string Hash(User user, string password) => _hasher.HashPassword(user, password);

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
