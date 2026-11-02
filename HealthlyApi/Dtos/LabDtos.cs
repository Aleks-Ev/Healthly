using System.ComponentModel.DataAnnotations;
using HealthlyApi.Models;

namespace HealthlyApi.Dtos;

// Лаборатория-партнёр
public record LaboratoryResponse(int Id, string Name, string City, string Address, int StaffCount);

// POST /api/laboratories (Admin)
public class LaboratoryRequest
{
    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, AllowedValues(
        TestingCities.Istanbul, TestingCities.Ankara, TestingCities.Izmir, TestingCities.Antalya,
        TestingCities.Bursa, TestingCities.Adana, TestingCities.Gaziantep, TestingCities.Konya,
        ErrorMessage = "Select a testing city")]
    public string City { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Address { get; set; }
}

// GET /api/lab/patients/{code} — минимум, нужный сотруднику, чтобы убедиться, что перед ним тот самый пациент
// (без телефона, e-mail и истории анализов)
public record LabPatientResponse(int Id, string PatientCode, string FullName, DateOnly? DateOfBirth, string Gender);

// GET /api/lab/documents — очередь лаборатории
public record LabDocumentResponse(
    int Id,
    int PatientId,
    string PatientName,
    string PatientCode,
    string OriginalFileName,
    DateTime UploadDate,
    string UploadedBy,
    string AnalysisStatus,  // LabAnalysisStatuses: NotAnalyzed, Draft, Confirmed
    int ResultsCount,
    int CriticalCount);

public static class LabAnalysisStatuses
{
    public const string NotAnalyzed = "NotAnalyzed";
    public const string Draft = "Draft";
    public const string Confirmed = "Confirmed";
}

// GET /api/documents/{id}/history — журнал действий с документом
public record AuditEventResponse(DateTime At, string UserName, string UserRole, string? LaboratoryName, string Action, string Details);
