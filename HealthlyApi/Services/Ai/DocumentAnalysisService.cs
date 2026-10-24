using System.Globalization;
using Microsoft.EntityFrameworkCore;
using HealthlyApi.Data;
using HealthlyApi.Dtos;
using HealthlyApi.Models;

namespace HealthlyApi.Services.Ai;

// Оркестратор анализа документа (POST /api/documents/{id}/analyze):
// 1. открывает файл документа и загружает справочник норм;
// 2. передаёт всё в IAiDocumentProcessor (сейчас заглушка, позже — реальный OCR + LLM);
// 3. сам, без ИИ, сравнивает найденные значения с нормами из БД: отклонение, категория, к какому врачу обратиться.
// Результат пока не сохраняется — только возвращается клиенту.
public class DocumentAnalysisService
{
    public const string Disclaimer =
        "This analysis is generated automatically and is not a medical diagnosis. Always discuss your results with a qualified healthcare professional.";

    private readonly AppDbContext _context;
    private readonly FileStorageService _storage;
    private readonly IAiDocumentProcessor _processor;

    public DocumentAnalysisService(AppDbContext context, FileStorageService storage, IAiDocumentProcessor processor)
    {
        _context = context;
        _storage = storage;
        _processor = processor;
    }

    // null — файла документа нет в хранилище
    public async Task<DocumentAnalysisResponse?> AnalyzeAsync(MedicalDocument document, User patient, CancellationToken cancellationToken)
    {
        await using var content = _storage.OpenRead(document.FilePath);
        if (content == null)
        {
            return null;
        }

        var references = await _context.ReferenceMedicalValues.AsNoTracking().ToListAsync(cancellationToken);

        var input = new AiDocumentInput(
            document.Id,
            document.OriginalFileName,
            document.ContentType,
            content,
            new AiPatientContext(patient.FullName, patient.Gender, GetAge(patient.DateOfBirth)),
            references);

        var analysis = await _processor.AnalyzeAsync(input, cancellationToken);

        var indicators = analysis.Indicators.Select(indicator => Evaluate(indicator, references)).ToList();
        var specialists = indicators
            .Where(i => i.IsAbnormal == true && !string.IsNullOrEmpty(i.DoctorSpecialty))
            .Select(i => i.DoctorSpecialty!)
            .Distinct()
            .ToList();

        return new DocumentAnalysisResponse(
            document.Id,
            indicators,
            indicators.Count(i => i.IsAbnormal == true),
            analysis.Summary,
            analysis.Recommendations.ToList(),
            specialists,
            analysis.AnonymizedText,
            analysis.Model,
            analysis.IsMock,
            DateTime.UtcNow,
            Disclaimer);
    }

    // Норма из справочника важнее диапазона из бланка: так одинаковые показатели оцениваются одинаково
    private static AnalyzedIndicatorResponse Evaluate(AiExtractedIndicator indicator, IReadOnlyList<ReferenceMedicalValue> references)
    {
        var reference = references.FirstOrDefault(r => string.Equals(r.MetricName, indicator.TestName, StringComparison.OrdinalIgnoreCase));

        if (reference != null && decimal.TryParse(indicator.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            var isAbnormal = value < reference.MinNormal || value > reference.MaxNormal;
            return new AnalyzedIndicatorResponse(
                indicator.TestName,
                indicator.Value,
                string.IsNullOrEmpty(indicator.Unit) ? reference.Unit : indicator.Unit,
                FormatRange(reference),
                isAbnormal,
                reference.Category,
                reference.DoctorSpecialty,
                indicator.Confidence);
        }

        // Показателя нет в справочнике — оцениваем по диапазону из бланка (null, если и его не разобрать)
        return new AnalyzedIndicatorResponse(
            indicator.TestName,
            indicator.Value,
            indicator.Unit,
            indicator.ReferenceRange,
            ReferenceRangeEvaluator.IsAbnormal(indicator.Value, indicator.ReferenceRange),
            null,
            null,
            indicator.Confidence);
    }

    // В формате, который понимает ReferenceRangeEvaluator: "3.9-5.6", "<5.2", ">1.0"
    private static string FormatRange(ReferenceMedicalValue reference)
    {
        static string Format(decimal value) => value.ToString("0.###", CultureInfo.InvariantCulture);

        return (reference.MinNormal, reference.MaxNormal) switch
        {
            ({ } min, { } max) => $"{Format(min)}-{Format(max)}",
            (null, { } max) => $"<{Format(max)}",
            ({ } min, null) => $">{Format(min)}",
            _ => string.Empty,
        };
    }

    private static int? GetAge(DateOnly? dateOfBirth)
    {
        if (dateOfBirth is not { } birth)
        {
            return null;
        }
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - birth.Year;
        return birth > today.AddYears(-age) ? age - 1 : age;
    }
}
