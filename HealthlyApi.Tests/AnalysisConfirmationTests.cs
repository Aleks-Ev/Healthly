using HealthlyApi.Data;
using HealthlyApi.Dtos;
using HealthlyApi.Models;
using HealthlyApi.Services.Ai;

namespace HealthlyApi.Tests;

// Подтверждённые человеком показатели → TestResults (DocumentAnalysisService.BuildConfirmedResult):
// в историю и графики значения попадают в единицах справочника, с оценкой и критической отметкой
public class AnalysisConfirmationTests
{
    private static readonly Lazy<List<ReferenceMedicalValue>> Catalog =
        new(() => ReferenceValuesCsv.Load(Path.Combine(AppContext.BaseDirectory, "Data", "reference-values.csv")));

    [Theory]
    [InlineData("Hemoglobin", "12", "g/dL", "Hemoglobin", "120", "g/L", false)]
    [InlineData("WBC", "6.7", "10^3/uL", "Leukocytes", "6.7", "10^9/L", false)]
    [InlineData("Total Leucocyte Count", "5000", "/cumm", "Leukocytes", "5", "10^9/L", false)]
    [InlineData("Glucose", "126", "mg/dL", "Glucose", "6.994", "mmol/L", true)]
    public void Saves_values_in_the_catalog_units_so_charts_do_not_mix_units(
        string name, string value, string unit, string savedName, string savedValue, string savedUnit, bool abnormal)
    {
        var result = Confirm(name, value, unit);

        Assert.Equal(savedName, result.TestName);
        Assert.Equal(savedValue, result.Value);
        Assert.Equal(savedUnit, result.Unit);
        Assert.Equal(abnormal, result.IsAbnormal);
        Assert.False(result.IsCritical);
    }

    [Fact]
    public void Critical_value_is_saved_as_critical()
    {
        var result = Confirm("Potassium", "6.8", "mmol/L");

        Assert.True(result.IsCritical);
        Assert.True(result.IsAbnormal);
        Assert.Equal("3.5-5.1", result.ReferenceRange);
    }

    [Fact]
    public void Value_confirmed_by_a_person_is_trusted()
    {
        // При анализе «44» выглядело как потерянная точка; человек подтвердил 44 — сохраняем как есть
        var result = Confirm("Albumin", "4.4", "g/dL");

        Assert.Equal("4.4", result.Value);
        Assert.False(result.IsAbnormal);
    }

    [Fact]
    public void Indicator_without_a_norm_is_saved_as_printed_on_the_report()
    {
        var result = Confirm("PDW", "9", "fL", printedRange: "9-17");

        Assert.Equal("PDW", result.TestName);
        Assert.Equal("9", result.Value);
        Assert.Equal("9-17", result.ReferenceRange);
        Assert.False(result.IsAbnormal);
        Assert.False(result.IsCritical);
    }

    private static TestResult Confirm(string name, string value, string unit, string printedRange = "") =>
        DocumentAnalysisService.BuildConfirmedResult(
            new ConfirmedIndicatorRequest { TestName = name, Value = value, Unit = unit, ReferenceRange = printedRange },
            Catalog.Value, Genders.Female, 40);
}
