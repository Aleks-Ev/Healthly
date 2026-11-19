using HealthlyApi.Models;

namespace HealthlyApi.Dtos;

// GET /api/reference-values — одна строка справочника норм (только чтение, для врачей и админа).
// IsReviewed = false — строка-черновик: врач-консультант её ещё не проверил (пустой ReviewedBy в CSV)
public record ReferenceValueResponse(
    int Id,
    string MetricName,
    string[] Aliases,
    string Sex,
    int? AgeFrom,
    int? AgeTo,
    decimal? MinNormal,
    decimal? MaxNormal,
    decimal? CriticalLow,
    decimal? CriticalHigh,
    string Unit,
    string Category,
    string DoctorSpecialty,
    string Source,
    bool IsReviewed,
    string? ReviewedBy,
    DateOnly? ReviewedAt)
{
    public static ReferenceValueResponse From(ReferenceMedicalValue value) => new(
        value.Id,
        value.MetricName,
        value.Aliases.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
        value.Sex,
        value.AgeFrom,
        value.AgeTo,
        value.MinNormal,
        value.MaxNormal,
        value.CriticalLow,
        value.CriticalHigh,
        value.Unit,
        value.Category,
        value.DoctorSpecialty,
        value.Source,
        !string.IsNullOrWhiteSpace(value.ReviewedBy),
        string.IsNullOrWhiteSpace(value.ReviewedBy) ? null : value.ReviewedBy,
        value.ReviewedAt);
}
