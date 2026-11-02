namespace HealthlyApi.Models;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty; // Юридическое имя: "First Last"
    public string Role { get; set; } = "Patient"; // Роли: Patient, Doctor, Admin
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Новые медицинские метрики (с вопросительным знаком, так как они опциональны)
    public double? Height { get; set; } // Рост (например, в см: 175.5)
    public double? Weight { get; set; } // Вес (например, в кг: 70.2)
    public string Gender { get; set; } = string.Empty; // Пол / Гендер (Genders)

    // Данные из формы регистрации
    public string PhoneNumber { get; set; } = string.Empty; // В формате +905321234567
    public DateOnly? DateOfBirth { get; set; }
    public string City { get; set; } = string.Empty; // Город сдачи анализов (TestingCities)
    public string? AccessCode { get; set; } // Необязательный код доступа (промо/партнёрский)
    public bool MarketingOptIn { get; set; } // Согласие на маркетинговые сообщения
    public DateTime? ConsentsAcceptedAt { get; set; } // Когда приняты Privacy Policy/Terms/Authorization

    // Healthly ID пациента ("HL-7K3P9Q"): пациент называет его или показывает QR-код в лаборатории,
    // сотрудник по нему находит пациента. Есть только у пациентов (PatientCodes)
    public string? PatientCode { get; set; }

    // Для роли LabStaff — лаборатория, от имени которой работает сотрудник
    public int? LaboratoryId { get; set; }
    public Laboratory? Laboratory { get; set; }
}
