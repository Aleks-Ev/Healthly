using HealthlyApi.Data;
using HealthlyApi.Models;
using HealthlyApi.Services;
using HealthlyApi.Services.Ai;

namespace HealthlyApi.Tests;

// Справочник норм из Data/reference-values.csv: чтение, проверка ошибок, выбор нормы по полу/возрасту, критические значения
public class ReferenceValuesTests
{
    private const string Header = "Metric,Aliases,Sex,AgeFrom,AgeTo,Min,Max,CriticalLow,CriticalHigh,Unit,Category,Specialty,Source,ReviewedBy,ReviewedAt";

    private static readonly Lazy<List<ReferenceMedicalValue>> RealCatalog =
        new(() => ReferenceValuesCsv.Load(Path.Combine(AppContext.BaseDirectory, "Data", "reference-values.csv")));

    [Fact]
    public void The_real_reference_file_is_valid()
    {
        var values = RealCatalog.Value;

        Assert.True(values.Count >= 50);
        Assert.Contains(values, v => v.MetricName == "Hemoglobin" && v.Sex == Genders.Female);
        Assert.Contains(values, v => v.MetricName == "Potassium" && v.CriticalHigh != null);
    }

    [Fact]
    public void Reads_a_file_resaved_by_excel_with_semicolons_and_decimal_commas()
    {
        var text = "﻿" + Header.Replace(',', ';') + "\nGlucose;GLU|Fasting glucose;Any;18;;3,9;5,6;2,5;25;mmol/L;Biochemistry;Endocrinologist;\"ADA; fasting\";Dr. Test;2026-11-02\n";

        var value = Assert.Single(ReferenceValuesCsv.Parse(text));

        Assert.Equal(3.9m, value.MinNormal);
        Assert.Equal(25m, value.CriticalHigh);
        Assert.Equal("ADA; fasting", value.Source);
        Assert.Equal(new DateOnly(2026, 11, 2), value.ReviewedAt);
        Assert.Equal(["Glucose", "GLU", "Fasting glucose"], value.AllNames());
    }

    [Theory]
    [InlineData("Glucose,,Any,18,,5.6,3.9,,,mmol/L,Biochemistry,Endocrinologist,,,", "Min 5.6 is greater than Max 3.9")]
    [InlineData("Glucose,,Any,18,,3.9,5.6,4.0,,mmol/L,Biochemistry,Endocrinologist,,,", "CriticalLow 4.0 must be below the normal range")]
    [InlineData("Glucose,,Kid,18,,3.9,5.6,,,mmol/L,Biochemistry,Endocrinologist,,,", "Sex \"Kid\"")]
    [InlineData("Glucose,,Any,18,,abc,5.6,,,mmol/L,Biochemistry,Endocrinologist,,,", "Min \"abc\" is not a number")]
    [InlineData("Glucose,,Any,18,,3.9,5.6,,,,Biochemistry,Endocrinologist,,,", "Unit, Category and Specialty are required")]
    public void Reports_mistakes_with_the_line_number(string row, string expected)
    {
        var error = Assert.Throws<InvalidDataException>(() => ReferenceValuesCsv.Parse($"{Header}\n{row}\n"));

        Assert.Contains("line 2", error.Message);
        Assert.Contains(expected, error.Message);
    }

    [Fact]
    public void Rows_that_apply_to_the_same_person_are_an_error()
    {
        var text = $"{Header}\nHemoglobin,,Any,18,,120,160,,,g/L,CBC,Hematologist,,,\nHemoglobin,,Female,18,,120,155,,,g/L,CBC,Hematologist,,,\n";

        var error = Assert.Throws<InvalidDataException>(() => ReferenceValuesCsv.Parse(text));

        Assert.Contains("lines 2 and 3", error.Message);
    }

    [Fact]
    public void A_name_used_by_two_metrics_is_an_error()
    {
        var text = $"{Header}\nLeukocytes,WBC,Any,18,,4,10,,,10^9/L,CBC,Hematologist,,,\nWhite cells,WBC,Any,18,,4,10,,,10^9/L,CBC,Hematologist,,,\n";

        var error = Assert.Throws<InvalidDataException>(() => ReferenceValuesCsv.Parse(text));

        Assert.Contains("\"WBC\" is used by two metrics", error.Message);
    }

    [Theory]
    [InlineData("Female", 34, false)]   // 125 g/L: норма для женщин (120-155)…
    [InlineData("Male", 34, true)]      // …и ниже нормы для мужчин (130-170)
    public void Picks_the_norm_for_the_patients_sex(string sex, int age, bool abnormal)
    {
        var result = Evaluate("Hemoglobin", "125", "g/L", sex, age);

        Assert.Equal(abnormal, result.IsAbnormal);
    }

    [Fact]
    public void Without_a_norm_for_this_patient_uses_the_printed_range_but_keeps_the_specialist()
    {
        // Норм для детей в справочнике нет → диапазон из бланка; категория и врач известны по названию
        var child = Evaluate("Hemoglobin", "115", "g/L", Genders.Female, 8, printedRange: "110-140");

        Assert.False(child.IsAbnormal);
        Assert.Equal("110-140", child.ReferenceRange);
        Assert.Equal("Hematologist", child.DoctorSpecialty);
    }

    [Theory]
    [InlineData("WBC", "Leukocytes")]
    [InlineData("Total Leucocyte Count", "Leukocytes")]
    [InlineData("SGPT", "ALT")]
    [InlineData("PROTEIN, TOTAL", "Total protein")]
    [InlineData("Üre", "Urea")]
    public void Aliases_map_to_the_catalog_name(string printed, string catalogName)
    {
        Assert.Equal(catalogName, Evaluate(printed, "1", "", Genders.Female, 30).TestName);
    }

    [Theory]
    [InlineData("Potassium", "6.8", "mmol/L", true)]
    [InlineData("Potassium", "2.1", "mEq/L", true)]
    [InlineData("Potassium", "5.6", "mmol/L", false)]   // выше нормы, но не критично
    [InlineData("Glucose", "500", "mg/dL", true)]        // 27.8 mmol/L
    [InlineData("Hemoglobin", "6.2", "g/dL", true)]      // 62 g/L
    [InlineData("Platelets", "15000", "/cumm", true)]    // 15 ×10⁹/L
    public void Critical_values_are_flagged(string name, string value, string unit, bool critical)
    {
        var result = Evaluate(name, value, unit, Genders.Female, 40);

        Assert.Equal(critical, result.IsCritical);
        Assert.True(result.IsAbnormal);
    }

    [Fact]
    public void A_critical_value_that_may_be_an_ocr_error_is_still_shown_but_must_be_checked()
    {
        // «44» — возможно, 4.4 с потерянной точкой; но если это правда 44 — опасно. Не прячем, просим сверить
        var result = Evaluate("Potassium", "44", "mmol/L", Genders.Female, 40);

        Assert.True(result.IsCritical);
        Assert.True(result.IsAbnormal);
        Assert.Equal(DocumentAnalysisService.UncertainConfidence, result.Confidence);
    }

    [Theory]
    [InlineData("Creatinine", "80", "µmol/L", false)]   // 0.90 mg/dL
    [InlineData("Creatinine", "150", "umol/L", true)]    // 1.70 mg/dL
    [InlineData("Vitamin D", "75", "nmol/L", false)]     // 30.0 ng/mL
    [InlineData("TSH", "2.1", "µIU/mL", false)]
    [InlineData("Hematocrit", "0.31", "L/L", true)]      // 31 %
    [InlineData("Ferritin", "10", "µg/L", true)]         // 10 ng/mL
    public void Converts_units_of_the_full_panel(string name, string value, string unit, bool abnormal)
    {
        Assert.Equal(abnormal, Evaluate(name, value, unit, Genders.Female, 40).IsAbnormal);
    }

    [Fact]
    public void Calcium_in_milliequivalents_is_not_converted()
    {
        // У кальция 1 mEq = 0.5 mmol — простая подмена единиц дала бы ошибку в 2 раза; берём диапазон из бланка
        Assert.Null(UnitConverter.GetFactor("Calcium", "mEq/L", "mg/dL"));
    }

    private static Dtos.AnalyzedIndicatorResponse Evaluate(
        string name, string value, string unit, string sex, int age, string printedRange = "") =>
        DocumentAnalysisService.Evaluate(new AiExtractedIndicator(name, value, unit, printedRange, 0.95), RealCatalog.Value, sex, age);
}
