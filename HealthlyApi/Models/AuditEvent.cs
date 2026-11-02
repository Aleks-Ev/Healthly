namespace HealthlyApi.Models;

// Журнал действий с медицинскими документами: кто, когда и что сделал (загрузил, запустил анализ, подтвердил…).
// Записи только добавляются (не меняются и не удаляются вместе с пользователем: имя сохраняется строкой),
// чтобы по ним можно было восстановить историю документа
public class AuditEvent
{
    public long Id { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;

    public int? UserId { get; set; }                     // кто сделал (null — система)
    public string UserName { get; set; } = string.Empty;  // имя на момент действия
    public string UserRole { get; set; } = string.Empty;
    public int? LaboratoryId { get; set; }                 // от имени какой лаборатории (для LabStaff)

    public string Action { get; set; } = string.Empty;    // AuditActions
    public int? DocumentId { get; set; }
    public int? PatientId { get; set; }
    public string Details { get; set; } = string.Empty;   // короткое пояснение без медданных: "photo.jpg", "4 indicators"
}

public static class AuditActions
{
    public const string PatientLookedUp = "PatientLookedUp";
    public const string DocumentUploaded = "DocumentUploaded";
    public const string DocumentAnalyzed = "DocumentAnalyzed";
    public const string AnalysisConfirmed = "AnalysisConfirmed";
    public const string DocumentDeleted = "DocumentDeleted";
}
