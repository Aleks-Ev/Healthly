namespace HealthlyApi.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = "Patient"; // Роли: Patient, Doctor, Admin
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Новые медицинские метрики (с вопросительным знаком, так как они опциональны)
    public double? Height { get; set; } // Рост (например, в см: 175.5)
    public double? Weight { get; set; } // Вес (например, в кг: 70.2)
    public string Gender { get; set; } = string.Empty; // Пол / Гендер
}