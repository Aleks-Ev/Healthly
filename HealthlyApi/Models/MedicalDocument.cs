namespace HealthlyApi.Models;

public class MedicalDocument
{
    public int Id { get; set; }
    
    // Внешний ключ на пользователя, который загрузил документ
    public int UserId { get; set; }
    public User? User { get; set; } // Навигационное свойство для связи

    public string OriginalFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime UploadDate { get; set; } = DateTime.UtcNow;
    
    // Статус обработки: "Pending", "Processing", "Completed", "Error"
    public string Status { get; set; } = "Pending";

    // Коллекция связанных результатов анализов (один документ -> много показателей)
    public List<TestResult> TestResults { get; set; } = new();
}