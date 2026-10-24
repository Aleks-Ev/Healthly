using System.ComponentModel.DataAnnotations;
using HealthlyApi.Models;

namespace HealthlyApi.Dtos;

// Документ в списке (без показателей)
public record DocumentResponse(
    int Id,
    int UserId,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    DateTime UploadDate,
    string Status,
    int ResultsCount,
    int AbnormalCount);

// Документ вместе с показателями
public record DocumentDetailsResponse(
    int Id,
    int UserId,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    DateTime UploadDate,
    string Status,
    List<TestResultResponse> Results);

// PUT /api/documents/{id}/status (Doctor, Admin)
public class UpdateDocumentStatusRequest
{
    [Required, AllowedValues(DocumentStatuses.Pending, DocumentStatuses.Processing, DocumentStatuses.Completed, DocumentStatuses.Error)]
    public string Status { get; set; } = string.Empty;
}

public record TestResultResponse(
    int Id,
    int MedicalDocumentId,
    string TestName,
    string Value,
    string Unit,
    string ReferenceRange,
    bool IsAbnormal);

// POST /api/documents/{id}/results и PUT /api/results/{id}
public class TestResultRequest
{
    [Required, MaxLength(100)]
    public string TestName { get; set; } = string.Empty; // "Hemoglobin"

    [Required, MaxLength(50)]
    public string Value { get; set; } = string.Empty;    // "135"

    [MaxLength(30)]
    public string? Unit { get; set; }                    // "g/L"

    [MaxLength(50)]
    public string? ReferenceRange { get; set; }          // "120-160", "<5", ">=1.2"

    // null = определить автоматически по ReferenceRange (ReferenceRangeEvaluator)
    public bool? IsAbnormal { get; set; }
}

public static class DocumentMappings
{
    public static DocumentDetailsResponse ToDetailsResponse(this MedicalDocument document) => new(
        document.Id,
        document.UserId,
        document.OriginalFileName,
        document.ContentType,
        document.FileSizeBytes,
        document.UploadDate,
        document.Status,
        document.TestResults.OrderBy(r => r.Id).Select(r => r.ToResponse()).ToList());

    public static TestResultResponse ToResponse(this TestResult result) => new(
        result.Id,
        result.MedicalDocumentId,
        result.TestName,
        result.Value,
        result.Unit,
        result.ReferenceRange,
        result.IsAbnormal);
}
