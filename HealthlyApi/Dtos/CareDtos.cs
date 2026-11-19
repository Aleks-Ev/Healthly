using System.ComponentModel.DataAnnotations;

namespace HealthlyApi.Dtos;

// --- Предложения врачей и лабораторий (GET /api/care/suggestions) ---

// PatientCount — у скольких пациентов врач уже ведёт наблюдение (активные + принятые связи);
// список отсортирован по нему: сначала менее загруженные
public record DoctorSuggestion(
    int Id,
    string FullName,
    string Specialty,
    string Workplace,
    string City,
    string Bio,
    string Languages,
    int PatientCount);

public record LaboratorySuggestion(
    int Id,
    string Name,
    string City,
    string Address,
    string Description,
    string OpeningHours,
    string Phone,
    int PatientCount);

public record CareSuggestionsResponse(string City, List<DoctorSuggestion> Doctors, List<LaboratorySuggestion> Laboratories);

// --- Связи (GET /api/care/connections) ---

// Пациент глазами врача/лаборатории: только то, что нужно, чтобы принять решение. Медицинских данных здесь нет.
// PatientCode (Healthly ID) — только при активной связи (лаборатории — чтобы загрузить бланк)
public record CarePatientResponse(int Id, string FullName, string City, string Gender, int? Age, string? PatientCode);

// Врач или лаборатория глазами пациента. Subtitle: у врача «специальность · место работы», у лаборатории — адрес
public record CareProviderResponse(int Id, string Name, string Subtitle, string City, string Details);

public record CareConnectionResponse(
    int Id,
    string ProviderType,   // Doctor | Laboratory
    string Status,         // Requested | Accepted | Active | Declined | Cancelled | Ended
    CarePatientResponse Patient,
    CareProviderResponse Provider,
    string PatientMessage,
    string ProviderNote,
    DateTime RequestedAt,
    DateTime? RespondedAt,
    DateTime? ConfirmedAt,
    DateTime? EndedAt);

// POST /api/care/connections — ровно одно из DoctorId / LaboratoryId
public class CreateCareConnectionRequest
{
    public int? DoctorId { get; set; }
    public int? LaboratoryId { get; set; }

    [MaxLength(300)]
    public string? Message { get; set; }
}

// POST /api/care/connections/{id}/decline и /end — необязательное пояснение
public class CareDecisionRequest
{
    [MaxLength(300)]
    public string? Note { get; set; }
}

// --- Уведомления ---

public record NotificationResponse(
    int Id,
    string Type,
    string ActorName,
    string Title,
    string Body,
    string Link,
    int? CareConnectionId,
    DateTime CreatedAt,
    DateTime? ReadAt);

public record UnreadCountResponse(int Count);
