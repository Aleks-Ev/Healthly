namespace HealthlyApi.Models;

public class MedicalDocument
{
    public int Id { get; set; }

    // Внешний ключ на пользователя, который загрузил документ
    public int UserId { get; set; }
    public User? User { get; set; } // Навигационное свойство для связи

    public string OriginalFileName { get; set; } = string.Empty;
    // Имя файла в хранилище (GUID + расширение), а не полный путь — см. FileStorageService
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty; // MIME-тип, например "application/pdf"
    public long FileSizeBytes { get; set; }
    public DateTime UploadDate { get; set; } = DateTime.UtcNow;

    // Статус обработки: см. DocumentStatuses
    public string Status { get; set; } = DocumentStatuses.Pending;

    // Коллекция связанных результатов анализов (один документ -> много показателей)
    public List<TestResult> TestResults { get; set; } = new();
}
