using System.ComponentModel.DataAnnotations;
using HealthlyApi.Models;

namespace HealthlyApi.Dtos;

// POST /api/auth/register — публичная регистрация (всегда роль Patient)
public class RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    // Юридическое имя; в User.FullName сохраняется как "FirstName LastName"
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    // Турецкий номер: +90 и 10 цифр, пробелы/дефисы/скобки допускаются
    [Required, RegularExpression(@"^\+90[\s\-()]*(\d[\s\-()]*){10}$", ErrorMessage = "Enter a Turkish phone number in the format +90 5XX XXX XX XX")]
    public string PhoneNumber { get; set; } = string.Empty;

    // ISO-дата "YYYY-MM-DD"; возраст проверяется в контроллере
    [Required]
    public DateOnly? DateOfBirth { get; set; }

    [Required, AllowedValues(Genders.Female, Genders.Male, Genders.Other, ErrorMessage = "Select Female, Male or Other")]
    public string Gender { get; set; } = string.Empty;

    [Required, AllowedValues(
        TestingCities.Istanbul, TestingCities.Ankara, TestingCities.Izmir, TestingCities.Antalya,
        TestingCities.Bursa, TestingCities.Adana, TestingCities.Gaziantep, TestingCities.Konya,
        ErrorMessage = "Select a city where you will be testing")]
    public string City { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? AccessCode { get; set; }

    public bool MarketingOptIn { get; set; }

    // Обязательные согласия: Privacy Policy + Terms of Service + Request for Lab Results
    [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the Privacy Policy, Terms of Service and Request for Lab Results")]
    public bool AcceptTerms { get; set; }

    // Обязательное согласие: Authorization for Use of Medical Information
    [Range(typeof(bool), "true", "true", ErrorMessage = "You must accept the Authorization for Use of Medical Information")]
    public bool AcceptMedicalAuthorization { get; set; }
}

// POST /api/auth/login
public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

// POST /api/auth/refresh и /api/auth/logout.
// Мобильные клиенты передают токен в теле; веб-клиент — в httpOnly-cookie (тогда тело пустое: {})
public class RefreshRequest
{
    [MaxLength(200)]
    public string? RefreshToken { get; set; }
}

// PUT /api/auth/me/password — смена пароля самим пользователем
public class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string NewPassword { get; set; } = string.Empty;
}

// Ответ входа, регистрации, обновления токенов и смены пароля.
// Token — access-токен (JWT) для заголовка Authorization; RefreshToken — для POST /api/auth/refresh.
// RefreshToken = null, если он выдан в httpOnly-cookie (веб-клиент, см. RefreshTokenCookie).
public record AuthResponse(
    string Token,
    DateTime ExpiresAt,
    string? RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserResponse User);
