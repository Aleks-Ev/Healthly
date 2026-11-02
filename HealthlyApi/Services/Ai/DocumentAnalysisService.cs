using System.Globalization;
using System.Text.RegularExpressions;
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
public partial class DocumentAnalysisService
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

        var sex = patient.Gender;
        var age = GetAge(patient.DateOfBirth);
        var indicators = analysis.Indicators.Select(indicator => Evaluate(indicator, references, sex, age)).ToList();
        // Критическое значение — всегда отклонение (IsAbnormal = true), поэтому его врач тоже попадает в список
        var specialists = indicators
            .Where(i => i.IsAbnormal == true && !string.IsNullOrEmpty(i.DoctorSpecialty))
            .OrderByDescending(i => i.IsCritical)
            .Select(i => i.DoctorSpecialty!)
            .Distinct()
            .ToList();

        return new DocumentAnalysisResponse(
            document.Id,
            indicators,
            indicators.Count(i => i.IsAbnormal == true),
            indicators.Count(i => i.IsCritical),
            analysis.Summary,
            analysis.Recommendations.ToList(),
            specialists,
            analysis.AnonymizedText,
            analysis.Model,
            analysis.IsMock,
            DateTime.UtcNow,
            Disclaimer);
    }

    // Значение, прочитанное с низкой уверенностью (вероятная ошибка OCR) — клиент просит сверить его с бланком
    public const double UncertainConfidence = 0.4;

    // Норма из справочника важнее диапазона из бланка: так одинаковые показатели оцениваются одинаково.
    //   - строка справочника выбирается по названию или синониму, полу и возрасту пациента (FindReference);
    //   - единицы бланка пересчитываются в единицы справочника (UnitConverter): «Hemoglobin 12 g/dL» = 120 g/L — норма;
    //   - если строки для этого пола/возраста нет или единицу пересчитать нельзя — оцениваем по диапазону из бланка;
    //   - за критическими порогами справочника — IsCritical (красная подсветка, срочно к врачу).
    // public — для юнит-тестов (HealthlyApi.Tests)
    public static AnalyzedIndicatorResponse Evaluate(
        AiExtractedIndicator indicator, IReadOnlyList<ReferenceMedicalValue> references, string? sex = null, int? age = null)
    {
        var (metric, reference) = FindReference(references, indicator.TestName, sex, age);
        var range = indicator.ReferenceRange;
        bool? isAbnormal;
        var isCritical = false;

        if (reference != null
            && ReferenceRangeEvaluator.TryParse(indicator.Value, out var value)
            && UnitConverter.GetFactor(reference.MetricName, indicator.Unit, reference.Unit) is { } factor)
        {
            var converted = value * factor;
            isAbnormal = converted < (double?)reference.MinNormal || converted > (double?)reference.MaxNormal;
            isCritical = converted < (double?)reference.CriticalLow || converted > (double?)reference.CriticalHigh;
            // Диапазон показываем в единицах бланка — рядом со значением, как его видит пользователь
            range = FormatRange(reference, factor);
        }
        else
        {
            isAbnormal = ReferenceRangeEvaluator.IsAbnormal(indicator.Value, indicator.ReferenceRange);
        }

        var confidence = indicator.Confidence;
        if ((isAbnormal == true && LooksLikeLostDecimalPoint(indicator.Value, range)) || IsAmbiguousRange(indicator.Value, range))
        {
            // «48» при норме 3.6-5.1: OCR почти наверняка потерял точку (4.8). Не пугаем «отклонением» — просим сверить.
            // То же, если точка могла потеряться в самом диапазоне и от этого зависит вывод.
            // Критическое значение не прячем даже так: ложная тревога с пометкой «сверьте» лучше пропущенной опасности
            confidence = Math.Min(confidence, UncertainConfidence);
            if (!isCritical)
            {
                isAbnormal = null;
            }
        }

        return new AnalyzedIndicatorResponse(
            metric?.MetricName ?? indicator.TestName, // имя из справочника — одинаковое во всех отчётах (для истории и графиков)
            indicator.Value,
            string.IsNullOrEmpty(indicator.Unit) && reference != null ? reference.Unit : indicator.Unit,
            range,
            isAbnormal,
            isCritical,
            metric?.Category,
            metric?.DoctorSpecialty,
            confidence);
    }

    // Metric — любая строка этого показателя (название, категория, врач известны, даже если норма для пациента не подошла);
    // Reference — строка, подходящая по полу и возрасту, или null (тогда — диапазон из бланка).
    // Строка для конкретного пола точнее общей ("Any"). Пол "Other" или неизвестный — только общие строки
    private static (ReferenceMedicalValue? Metric, ReferenceMedicalValue? Reference) FindReference(
        IReadOnlyList<ReferenceMedicalValue> references, string testName, string? sex, int? age)
    {
        static bool Same(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        var rows = references.Where(r => r.AllNames().Any(n => Same(n, testName))).ToList();
        if (rows.Count == 0)
        {
            // Модель иногда дописывает к названию единицу: «Hemoglobin (g/L)» — пробуем без скобок
            var withoutParentheses = TrailingParenthesesRegex().Replace(testName, string.Empty);
            rows = references.Where(r => r.AllNames().Any(n => Same(n, withoutParentheses))).ToList();
        }

        var reference = rows
            .Where(r => r.Sex == ReferenceMedicalValue.AnySex || (sex != null && Same(r.Sex, sex)))
            .Where(r => age is null || ((r.AgeFrom ?? 0) <= age && age <= (r.AgeTo ?? int.MaxValue)))
            .OrderByDescending(r => r.Sex != ReferenceMedicalValue.AnySex)
            .FirstOrDefault();
        return (rows.FirstOrDefault(), reference);
    }

    // Целое значение вне диапазона с дробными границами, которое после деления на 10 или 100 попадает в диапазон.
    // Дробные границы обязательны: «Leukocytes 50» при норме 4-9 — настоящее сильное отклонение, его не трогаем
    private static bool LooksLikeLostDecimalPoint(string value, string range)
    {
        if (value.Contains('.') || value.Contains(',') || !ReferenceRangeEvaluator.TryParse(value, out var number))
        {
            return false;
        }
        var (min, max) = ReferenceRangeEvaluator.GetBounds(range);
        if (min is null || max is null || (min % 1 == 0 && max % 1 == 0))
        {
            return false;
        }
        return new[] { number / 10, number / 100 }.Any(candidate => candidate >= min && candidate <= max);
    }

    // «31.5-345»: у одной границы есть дробная часть, у другой нет — OCR мог потерять точку (34.5), а мог и нет
    // («0.5-10» бывает и настоящим). Если при двух прочтениях вывод о норме разный — решать за пользователя нельзя
    private static bool IsAmbiguousRange(string value, string range)
    {
        var match = MixedDecimalsRangeRegex().Match(range);
        if (!match.Success)
        {
            return false;
        }
        var minDecimals = match.Groups["minFraction"].Length;
        var maxDecimals = match.Groups["maxFraction"].Length;
        if ((minDecimals == 0) == (maxDecimals == 0))
        {
            return false; // точки у обеих границ или ни у одной — диапазон читается однозначно
        }

        var min = double.Parse(match.Groups["min"].Value, CultureInfo.InvariantCulture);
        var max = double.Parse(match.Groups["max"].Value, CultureInfo.InvariantCulture);
        var (otherMin, otherMax) = minDecimals > 0
            ? (min, max / Math.Pow(10, minDecimals))
            : (min / Math.Pow(10, maxDecimals), max);
        if (otherMin >= otherMax)
        {
            return false; // «4.5-11» → «4.5-1.1» не диапазон — значит, напечатано именно 4.5-11
        }

        var other = FormattableString.Invariant($"{otherMin}-{otherMax}");
        return ReferenceRangeEvaluator.IsAbnormal(value, range) != ReferenceRangeEvaluator.IsAbnormal(value, other);
    }

    [GeneratedRegex(@"\s*\([^()]*\)\s*$")]
    private static partial Regex TrailingParenthesesRegex();

    [GeneratedRegex(@"^\s*(?<min>\d+(?:\.(?<minFraction>\d+))?)\s*[-–—]\s*(?<max>\d+(?:\.(?<maxFraction>\d+))?)")]
    private static partial Regex MixedDecimalsRangeRegex();

    // В формате, который понимает ReferenceRangeEvaluator: "3.9-5.6", "<5.2", ">1.0"; границы — в единицах бланка
    private static string FormatRange(ReferenceMedicalValue reference, double factor)
    {
        string Format(decimal bound) => ((double)bound / factor).ToString("0.##", CultureInfo.InvariantCulture);

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
