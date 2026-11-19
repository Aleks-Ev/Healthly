namespace HealthlyApi.Models;

// Связь «пациент ↔ врач» или «пациент ↔ лаборатория».
// Путь: пациент выбирает из предложенных (по своему городу) и отправляет запрос (Requested) →
// врач / сотрудник лаборатории принимает (Accepted) или отклоняет (Declined) →
// пациент получает уведомление и подтверждает, что делится данными (Active) — или отказывается (Cancelled).
// Active-связь с врачом даёт ему доступ к медкарте пациента (CareAccess); лаборатория доступа к данным не получает —
// она только видит пациента в своём списке и загружает ему новые бланки.
// Закончить связь (Ended) может пациент в любой момент и врач/лаборатория — доступ пропадает сразу.
public class CareConnection
{
    public int Id { get; set; }

    public int PatientId { get; set; }
    public User? Patient { get; set; }

    // CareProviderTypes: Doctor (заполнен DoctorId) или Laboratory (заполнен LaboratoryId)
    public string ProviderType { get; set; } = CareProviderTypes.Doctor;
    public int? DoctorId { get; set; }
    public User? Doctor { get; set; }
    public int? LaboratoryId { get; set; }
    public Laboratory? Laboratory { get; set; }

    public string Status { get; set; } = CareConnectionStatuses.Requested;

    // Короткое сообщение пациента к запросу и причина отказа от врача/лаборатории (необязательные)
    public string PatientMessage { get; set; } = string.Empty;
    public string ProviderNote { get; set; } = string.Empty;

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RespondedAt { get; set; }      // врач/лаборатория ответили
    public int? RespondedByUserId { get; set; }     // кто именно (у лаборатории несколько сотрудников)
    public DateTime? ConfirmedAt { get; set; }      // пациент разрешил доступ
    public DateTime? EndedAt { get; set; }          // отменено / завершено
    public int? EndedByUserId { get; set; }
}

public static class CareProviderTypes
{
    public const string Doctor = "Doctor";
    public const string Laboratory = "Laboratory";
}

public static class CareConnectionStatuses
{
    public const string Requested = "Requested"; // ждёт ответа врача/лаборатории
    public const string Accepted = "Accepted";   // принят, ждёт подтверждения пациента
    public const string Active = "Active";       // пациент поделился данными
    public const string Declined = "Declined";   // врач/лаборатория отказали
    public const string Cancelled = "Cancelled"; // пациент отозвал запрос или не стал делиться данными
    public const string Ended = "Ended";         // активная связь завершена

    // «Открытые» статусы: пока связь в одном из них, второй запрос тому же врачу/лаборатории не создаётся
    public static readonly string[] Open = [Requested, Accepted, Active];
}
