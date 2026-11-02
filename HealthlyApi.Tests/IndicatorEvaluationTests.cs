using HealthlyApi.Models;
using HealthlyApi.Services;
using HealthlyApi.Services.Ai;

namespace HealthlyApi.Tests;

// Оценка показателя «в норме / нет» по справочнику (DocumentAnalysisService.Evaluate). Случаи — из живого прогона
// публичных образцов бланков (GNU Solidario, Flabs, Quest): там до исправлений были ложные «отклонения»
public class IndicatorEvaluationTests
{
    private static readonly ReferenceMedicalValue[] Catalog =
    [
        new() { MetricName = "Glucose", MinNormal = 3.9m, MaxNormal = 5.6m, Unit = "mmol/L", Category = "Biochemistry", DoctorSpecialty = "Endocrinologist" },
        new() { MetricName = "Cholesterol", MinNormal = null, MaxNormal = 5.2m, Unit = "mmol/L", Category = "Lipid profile", DoctorSpecialty = "Cardiologist" },
        new() { MetricName = "Hemoglobin", MinNormal = 120m, MaxNormal = 160m, Unit = "g/L", Category = "Complete blood count", DoctorSpecialty = "Hematologist" },
        new() { MetricName = "Leukocytes", MinNormal = 4.0m, MaxNormal = 9.0m, Unit = "10^9/L", Category = "Complete blood count", DoctorSpecialty = "Hematologist" },
    ];

    [Theory]
    [InlineData("Hemoglobin", "12", "g/dL", false, "12-16")]          // 12 g/dL = 120 g/L — норма, а не отклонение
    [InlineData("Hemoglobin", "11", "g/dL", true, "12-16")]
    [InlineData("Glucose", "71", "mg/dL", false, "70.26-100.89")]    // 71 mg/dL = 3.94 mmol/L
    [InlineData("Glucose", "126", "mg/dL", true, "70.26-100.89")]
    [InlineData("Cholesterol", "240", "mg/dL", true, "<201.06")]
    [InlineData("Leukocytes", "5000", "/cumm", false, "4000-9000")]   // 5000/мм³ = 5 ×10⁹/л
    [InlineData("Leukocytes", "6.7", "10^3/uL", false, "4-9")]
    [InlineData("Leukocytes", "12.5", "x10³/µL", true, "4-9")]
    [InlineData("Hemoglobin", "135", "", false, "120-160")]           // единица не указана — как в справочнике
    public void Converts_lab_units_to_the_catalog_units(string name, string value, string unit, bool abnormal, string shownRange)
    {
        var result = DocumentAnalysisService.Evaluate(new AiExtractedIndicator(name, value, unit, "", 0.95), Catalog);

        Assert.Equal(abnormal, result.IsAbnormal);
        Assert.Equal(shownRange, result.ReferenceRange); // норма справочника — в единицах бланка
        Assert.NotNull(result.DoctorSpecialty);
    }

    [Theory]
    [InlineData("Hemoglobin (g/L)")]   // так ответил Gemini, скопировав формат списка из промпта
    [InlineData("hemoglobin")]
    public void Catalog_name_is_matched_and_normalized(string modelName)
    {
        var result = DocumentAnalysisService.Evaluate(new AiExtractedIndicator(modelName, "15", "g/dL", "13-17", 0.95), Catalog);

        Assert.Equal("Hemoglobin", result.TestName);
        Assert.Equal("Hematologist", result.DoctorSpecialty);
        Assert.Equal("12-16", result.ReferenceRange);
    }

    [Fact]
    public void Unknown_unit_falls_back_to_the_range_printed_on_the_report()
    {
        // OCR исказил единицу («10^3/uL» → «107 3/uL») — пересчитать нельзя, берём диапазон из бланка
        var result = DocumentAnalysisService.Evaluate(new AiExtractedIndicator("Leukocytes", "256", "107 3/uL", "150-450", 0.9), Catalog);

        Assert.False(result.IsAbnormal);
        Assert.Equal("150-450", result.ReferenceRange);
        Assert.Equal("Hematologist", result.DoctorSpecialty);
    }

    [Theory]
    [InlineData("48", "3.6-5.1")]   // Albumin 4.8 — OCR потерял точку
    [InlineData("44", "3.5-5.3")]   // Potassium 4.4
    [InlineData("54", "3.9-5.6")]   // Glucose 5.4
    public void Suspected_lost_decimal_point_is_not_reported_as_abnormal(string value, string range)
    {
        var result = DocumentAnalysisService.Evaluate(new AiExtractedIndicator("Albumin", value, "g/dL", range, 0.9), Catalog);

        Assert.Null(result.IsAbnormal);
        Assert.Equal(DocumentAnalysisService.UncertainConfidence, result.Confidence);
    }

    [Theory]
    [InlineData("37.50", "31.5-345")]   // MCHC: на бланке 31.5-34.5 — по искажённому диапазону вышла бы «норма»
    [InlineData("12", "11.6-140")]      // RDW-CV: при 11.6-14.0 норма, при 11.6-140 тоже — но см. следующий
    [InlineData("3", "0.5-10")]         // 0.5-10 или 0.5-1.0? — неизвестно, а вывод разный
    public void Range_that_may_have_lost_a_decimal_point_is_flagged_for_checking_when_it_matters(string value, string range)
    {
        var result = DocumentAnalysisService.Evaluate(new AiExtractedIndicator("MCHC", value, "g/dL", range, 0.95), Catalog);

        var same = ReferenceRangeEvaluator.IsAbnormal(value, range) == ReferenceRangeEvaluator.IsAbnormal(value, range.Replace("345", "34.5").Replace("140", "14.0").Replace("-10", "-1.0"));
        Assert.Equal(same ? false : null, result.IsAbnormal);
        Assert.Equal(same ? 0.95 : DocumentAnalysisService.UncertainConfidence, result.Confidence);
    }

    [Theory]
    [InlineData("6.7", "4.5-11")]       // «4.5-1.1» — не диапазон, значит, напечатано именно 4.5-11
    [InlineData("4.4", "3.5-5.3")]
    [InlineData("50", "40-80")]
    public void Unambiguous_ranges_are_trusted(string value, string range)
    {
        var result = DocumentAnalysisService.Evaluate(new AiExtractedIndicator("WBC", value, "", range, 0.95), Catalog);

        Assert.False(result.IsAbnormal);
        Assert.Equal(0.95, result.Confidence);
    }

    [Theory]
    [InlineData("50", "4-9")]       // целые границы — настоящее сильное отклонение
    [InlineData("4.8", "3.6-4.5")]  // есть точка — значение прочитано как есть
    [InlineData("15", "3.6-5.1")]   // 1.5 и 0.15 тоже вне диапазона
    public void Real_deviations_stay_abnormal(string value, string range)
    {
        var result = DocumentAnalysisService.Evaluate(new AiExtractedIndicator("Albumin", value, "g/dL", range, 0.9), Catalog);

        Assert.True(result.IsAbnormal);
        Assert.Equal(0.9, result.Confidence);
    }

    [Theory]
    [InlineData("16", "7-25 mg/dL", false)]
    [InlineData("30", "6-22 (calc)", true)]
    [InlineData("0.96", "0.50-0.97 mg/dL", false)]
    [InlineData("78", ">OR= 60", false)]
    [InlineData("60", "> or = 60 mL/min/1.73m2", false)]
    [InlineData("55", ">OR= 60", true)]
    [InlineData("2", "Up to 15", false)]
    [InlineData("16", "Upto 15", true)]
    [InlineData("300000", "150000 - 410000 /cumm", false)]
    public void Range_with_units_or_words_is_understood(string value, string range, bool abnormal)
    {
        Assert.Equal(abnormal, ReferenceRangeEvaluator.IsAbnormal(value, range));
    }
}
