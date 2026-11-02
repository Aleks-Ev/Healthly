using System.ComponentModel.DataAnnotations;

namespace HealthlyApi.Dtos;

// Общие правила полей профиля — одинаковые при регистрации, создании пользователя админом и редактировании профиля
public static class ProfileValidation
{
    // Турецкий номер: +90 и 10 цифр, пробелы/дефисы/скобки допускаются
    public const string TurkishPhonePattern = @"^\+90[\s\-()]*(\d[\s\-()]*){10}$";
    public const string TurkishPhoneMessage = "Enter a Turkish phone number in the format +90 5XX XXX XX XX";

    public const int MinAge = 18;
    public const int MaxAge = 120;
}

// Дата рождения: возраст от 18 до 120 лет на сегодня; null проходит (обязательность задаёт [Required] или контроллер)
public sealed class AdultDateOfBirthAttribute : ValidationAttribute
{
    public AdultDateOfBirthAttribute()
        : base($"Age must be between {ProfileValidation.MinAge} and {ProfileValidation.MaxAge} years")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value is not DateOnly dateOfBirth)
        {
            return true;
        }
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return dateOfBirth <= today.AddYears(-ProfileValidation.MinAge) && dateOfBirth >= today.AddYears(-ProfileValidation.MaxAge);
    }
}
