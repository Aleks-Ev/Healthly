namespace HealthlyApi.Models;

public class TestResult
{
    public int Id { get; set; }

    // Внешний ключ на медицинский документ, к которому относится этот показатель
    public int MedicalDocumentId { get; set; }
    public MedicalDocument? MedicalDocument { get; set; } // Навигационное свойство

    public string TestName { get; set; } = string.Empty;       // Например, "Гемоглобин"
    public string Value { get; set; } = string.Empty;          // Например, "135"
    public string Unit { get; set; } = string.Empty;           // Например, "г/л"
    public string ReferenceRange { get; set; } = string.Empty; // Например, "120-140"
    public bool IsAbnormal { get; set; } = false;              // Флаг отклонения от нормы
}