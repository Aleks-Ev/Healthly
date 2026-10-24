using System.Globalization;
using HealthlyApi.Models;

namespace HealthlyApi.Services.Ai;

// Заглушка ИИ-обработки: файл не читает, а «распознаёт» правдоподобный бланк анализа крови.
// Значения зависят от Id документа (один и тот же документ — всегда один и тот же результат, разные — разный),
// поэтому на фронтенде и в мобильном приложении видно и «нормальные», и «отклонения».
//
// Повторяет все шаги будущей реальной реализации: сырой текст с персональными данными → PiiAnonymizer →
// «модель» получает обезличенный текст → показатели + резюме.
public class MockAiDocumentProcessor : IAiDocumentProcessor
{
    public const string ModelName = "mock-ai-v1";

    // Базовые значения и разброс: (название, единицы, диапазон в бланке, базовое значение, разброс ±, знаков после запятой)
    private static readonly (string Name, string Unit, string LabRange, double Base, double Spread, int Decimals)[] Panel =
    [
        ("Glucose", "mmol/L", "3.9-5.6", 5.3, 1.0, 1),
        ("Cholesterol", "mmol/L", "<5.2", 5.0, 0.8, 1),
        ("Hemoglobin", "g/L", "120-160", 135, 20, 0),
        ("Leukocytes", "10^9/L", "4.0-9.0", 6.5, 3.0, 1),
    ];

    public async Task<AiDocumentAnalysis> AnalyzeAsync(AiDocumentInput input, CancellationToken cancellationToken)
    {
        // Имитация времени работы OCR и LLM
        await Task.Delay(TimeSpan.FromMilliseconds(300), cancellationToken);

        var random = new Random(input.DocumentId);
        var indicators = Panel
            .Select(item =>
            {
                var value = item.Base + (random.NextDouble() * 2 - 1) * item.Spread;
                var text = Math.Round(value, item.Decimals).ToString($"F{item.Decimals}", CultureInfo.InvariantCulture);
                var confidence = Math.Round(0.90 + random.NextDouble() * 0.09, 2);
                return new AiExtractedIndicator(item.Name, text, item.Unit, item.LabRange, confidence);
            })
            .ToList();

        // 1. «OCR»: сырой текст бланка — с персональными данными, как в настоящем документе
        var rawText = BuildRawText(input, indicators);

        // 2. Обезличивание перед отправкой в модель
        var anonymizedText = PiiAnonymizer.Anonymize(rawText, [input.Patient.FullName]);

        // 3. «LLM»: резюме и рекомендации по обезличенному тексту
        var outOfRange = indicators.Where(i => IsOutOfRange(i, input.ReferenceValues)).Select(i => i.TestName).ToList();

        return new AiDocumentAnalysis(
            indicators,
            BuildSummary(indicators.Count, outOfRange),
            BuildRecommendations(outOfRange),
            anonymizedText,
            ModelName,
            IsMock: true);
    }

    private static string BuildRawText(AiDocumentInput input, IReadOnlyList<AiExtractedIndicator> indicators)
    {
        var lines = new List<string>
        {
            "HEALTHLY PARTNER LABORATORY — COMPLETE BLOOD COUNT & BIOCHEMISTRY",
            $"Patient: {input.Patient.FullName}",
            "Date of birth: 18.04.1992",
            "Phone: +90 532 111 22 33",
            "Email: patient.contact@example.com",
            $"Sample collected: {DateTime.UtcNow.AddDays(-2):dd.MM.yyyy}",
            "",
            "TEST            RESULT    UNIT      REFERENCE",
        };
        lines.AddRange(indicators.Select(i => $"{i.TestName,-15} {i.Value,-9} {i.Unit,-9} {i.ReferenceRange}"));
        lines.Add("");
        lines.Add("Validated by: Dr. Ayse Demir");
        return string.Join('\n', lines);
    }

    private static bool IsOutOfRange(AiExtractedIndicator indicator, IReadOnlyList<ReferenceMedicalValue> references)
    {
        var reference = references.FirstOrDefault(r => string.Equals(r.MetricName, indicator.TestName, StringComparison.OrdinalIgnoreCase));
        if (reference == null || !decimal.TryParse(indicator.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return false;
        }
        return value < reference.MinNormal || value > reference.MaxNormal;
    }

    private static string BuildSummary(int total, IReadOnlyList<string> outOfRange) => outOfRange.Count == 0
        ? $"[Mock AI summary] All {total} indicators in this report are within the reference ranges. " +
          "Your results look balanced — keep up your current routine and repeat the tests as your clinician advises."
        : $"[Mock AI summary] {outOfRange.Count} of {total} indicators are outside the reference range: {string.Join(", ", outOfRange)}. " +
          "Small deviations are common and can depend on diet, sleep or recent illness. " +
          "Discuss these results with a doctor, who can tell whether further tests are needed.";

    private static List<string> BuildRecommendations(IReadOnlyList<string> outOfRange)
    {
        var recommendations = new List<string>();
        if (outOfRange.Contains("Glucose"))
        {
            recommendations.Add("Repeat a fasting glucose test and consider an HbA1c test.");
        }
        if (outOfRange.Contains("Cholesterol"))
        {
            recommendations.Add("Ask your doctor about a full lipid profile (LDL, HDL, triglycerides).");
        }
        if (outOfRange.Contains("Hemoglobin"))
        {
            recommendations.Add("Check iron, ferritin and vitamin B12 levels.");
        }
        if (outOfRange.Contains("Leukocytes"))
        {
            recommendations.Add("Repeat the complete blood count in 2–4 weeks, especially after a recent infection.");
        }
        recommendations.Add("Keep tracking your results over time in Healthly to see trends.");
        return recommendations;
    }
}
