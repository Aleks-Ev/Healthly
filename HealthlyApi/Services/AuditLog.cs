using System.Security.Claims;
using HealthlyApi.Data;
using HealthlyApi.Models;

namespace HealthlyApi.Services;

// Запись в журнал действий (AuditEvents) от имени текущего пользователя.
// Только добавляет запись в контекст — сохраняется вместе с основным действием (тем же SaveChanges/транзакцией),
// поэтому действие без записи в журнал (или наоборот) невозможно.
// В Details — только безопасные пояснения (имя файла, число показателей), без значений анализов
public class AuditLog
{
    private readonly AppDbContext _context;

    public AuditLog(AppDbContext context)
    {
        _context = context;
    }

    public void Add(ClaimsPrincipal user, string action, int? documentId = null, int? patientId = null, string details = "")
    {
        _context.AuditEvents.Add(new AuditEvent
        {
            UserId = user.GetUserId(),
            UserName = user.FindFirstValue("name") ?? string.Empty,
            UserRole = user.FindFirstValue("role") ?? string.Empty,
            LaboratoryId = user.GetLaboratoryId(),
            Action = action,
            DocumentId = documentId,
            PatientId = patientId,
            Details = details.Length > 500 ? details[..500] : details,
        });
    }
}
