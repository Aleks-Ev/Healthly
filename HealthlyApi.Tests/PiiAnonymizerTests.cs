using HealthlyApi.Services;

namespace HealthlyApi.Tests;

// Тесты обезличивания текста перед отправкой в ИИ. Запуск: dotnet test (в корне репозитория)
public class PiiAnonymizerTests
{
    [Fact]
    public void Masks_email()
    {
        var result = PiiAnonymizer.Anonymize("Contact: elif.kaya92@gmail.com for results");

        Assert.Equal($"Contact: {PiiAnonymizer.EmailMask} for results", result);
    }

    [Theory]
    [InlineData("+90 532 123 45 67")]   // Турция, международный формат
    [InlineData("+905321234567")]
    [InlineData("0 (532) 123-45-67")]   // Турция, местный формат
    [InlineData("+7 (912) 345-67-89")]  // Россия
    [InlineData("8 912 345 67 89")]
    public void Masks_turkish_and_russian_phone_numbers(string phone)
    {
        var result = PiiAnonymizer.Anonymize($"Phone: {phone}.");

        Assert.Equal($"Phone: {PiiAnonymizer.PhoneMask}.", result);
    }

    [Theory]
    [InlineData("18.04.1992")]
    [InlineData("18/04/92")]
    [InlineData("1992-04-18")]
    public void Masks_dates(string date)
    {
        var result = PiiAnonymizer.Anonymize($"Date of birth: {date}");

        Assert.Equal($"Date of birth: {PiiAnonymizer.DateMask}", result);
    }

    [Theory]
    [InlineData("Patient: Elif Kaya", "Patient: [REDACTED_NAME]")]
    [InlineData("ФИО: Иванов Иван Петрович", "ФИО: [REDACTED_NAME]")]
    [InlineData("Hasta Adı: Ayşe Yılmaz", "Hasta Adı: [REDACTED_NAME]")]
    [InlineData("Лечащий врач Петрова Анна Сергеевна", "Лечащий врач [REDACTED_NAME]")]
    [InlineData("Validated by Иванов И.И.", "Validated by [REDACTED_NAME]")]
    [InlineData("Validated by: Dr. Ayse Demir", "Validated by: Dr. [REDACTED_NAME]")]
    [InlineData("Onaylayan: Uzm. Dr. Ali Kaya", "Onaylayan: Uzm. Dr. [REDACTED_NAME]")]
    public void Masks_names_by_label_patronymic_and_initials(string text, string expected)
    {
        Assert.Equal(expected, PiiAnonymizer.Anonymize(text));
    }

    [Fact]
    public void Masks_known_names_anywhere_in_the_text()
    {
        var result = PiiAnonymizer.Anonymize("Kaya reported fatigue. Elif Kaya was advised to rest.", ["Elif Kaya"]);

        Assert.Equal("[REDACTED_NAME] reported fatigue. [REDACTED_NAME] was advised to rest.", result);
    }

    // Главное для анализа: показатели, единицы и референсные диапазоны должны дойти до ИИ без изменений
    [Fact]
    public void Keeps_lab_values_and_reference_ranges_intact()
    {
        const string labLines = """
            Glucose         5.4       mmol/L    3.9-5.6
            Hemoglobin      13.2      g/dL      12.5-15.5
            Leukocytes      6.8       10^9/L    4.0-9.0
            Cholesterol     4.9       mmol/L    <5.2
            """;

        Assert.Equal(labLines, PiiAnonymizer.Anonymize(labLines));
    }

    [Fact]
    public void Anonymizes_a_full_lab_report_header_without_touching_the_results()
    {
        const string report = """
            Patient: Elif Kaya
            Date of birth: 18.04.1992
            Phone: +90 532 111 22 33
            Email: elif.kaya@example.com
            Glucose 6.1 mmol/L 3.9-5.6
            Validated by: Dr. Ayse Demir
            """;

        var result = PiiAnonymizer.Anonymize(report, ["Elif Kaya"]);

        Assert.DoesNotContain("Ayse", result);
        Assert.DoesNotContain("Demir", result);
        Assert.DoesNotContain("Elif", result);
        Assert.DoesNotContain("Kaya", result);
        Assert.DoesNotContain("1992", result);
        Assert.DoesNotContain("532", result);
        Assert.DoesNotContain("example.com", result);
        Assert.Contains("Glucose 6.1 mmol/L 3.9-5.6", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("No personal data here.")]
    public void Leaves_text_without_personal_data_unchanged(string text)
    {
        Assert.Equal(text, PiiAnonymizer.Anonymize(text));
    }
}
