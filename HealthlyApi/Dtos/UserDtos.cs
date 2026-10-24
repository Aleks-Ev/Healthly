using System.ComponentModel.DataAnnotations;
using HealthlyApi.Models;

namespace HealthlyApi.Dtos;

// Ответ API — без PasswordHash
public record UserResponse(
    int Id,
    string Email,
    string FullName,
    string Role,
    double? Height,
    double? Weight,
    string Gender,
    string PhoneNumber,
    DateOnly? DateOfBirth,
    string City,
    DateTime CreatedAt);

// POST /api/users — создание пользователя администратором
public class CreateUserRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [Required, AllowedValues(Roles.Patient, Roles.Doctor, Roles.Admin)]
    public string Role { get; set; } = Roles.Patient;

    [Range(30, 300)]
    public double? Height { get; set; } // см

    [Range(1, 500)]
    public double? Weight { get; set; } // кг

    [MaxLength(50)]
    public string? Gender { get; set; }
}

// PUT /api/users/{id} — полное обновление профиля.
// Role и Password может менять только Admin (null = не менять).
public class UpdateUserRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string FullName { get; set; } = string.Empty;

    [Range(30, 300)]
    public double? Height { get; set; }

    [Range(1, 500)]
    public double? Weight { get; set; }

    [MaxLength(50)]
    public string? Gender { get; set; }

    [AllowedValues(Roles.Patient, Roles.Doctor, Roles.Admin, null)]
    public string? Role { get; set; }

    [MinLength(8), MaxLength(128)]
    public string? Password { get; set; }
}

public static class UserMappings
{
    public static UserResponse ToResponse(this User user) => new(
        user.Id,
        user.Email,
        user.FullName,
        user.Role,
        user.Height,
        user.Weight,
        user.Gender,
        user.PhoneNumber,
        user.DateOfBirth,
        user.City,
        user.CreatedAt);

    // "+90 (532) 123-45-67" → "+905321234567"
    public static string NormalizePhone(string phone) => "+" + new string(phone.Where(char.IsDigit).ToArray());

    // Email храним в нижнем регистре, чтобы уникальный индекс был регистронезависимым
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
