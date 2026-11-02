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

    // Ошибки OCR в имени: цифра вместо буквы, потерянная диакритика, лишняя/пропущенная буква
    [Theory]
    [InlineData("Elif Kaya", "seen by E1if Kaya today", "E1if")]
    [InlineData("Elif Kaya", "seen by Elif Kava today", "Kava")]
    [InlineData("Ayşe Yılmaz", "seen by Ayse Yilmaz today", "Yilmaz")]
    [InlineData("Ayşe Yılmaz", "seen by Ayşe Yılrnaz today", "Yılrnaz")]
    [InlineData("Мария Смирнова", "seen by Мария Смирн0ва today", "Смирн0ва")]
    public void Masks_ocr_misreadings_of_known_names(string knownName, string text, string misreading)
    {
        var result = PiiAnonymizer.Anonymize(text, [knownName]);

        Assert.DoesNotContain(misreading, result);
        Assert.StartsWith("seen by", result);
        Assert.EndsWith("today", result);
    }

    [Theory]
    [InlineData("Patient ID : PN2", "Patient ID : ")]
    [InlineData("Patient ID PAC001", "Patient ID ")]
    [InlineData("Patient 10 PAC001", "Patient 10 ")]          // OCR прочитал «ID» как «10»
    [InlineData("Report ID : RE1", "Report ID : ")]
    [InlineData("Report I1D : RE1", "Report I1D : ")]       // так OCR прочитал бланк Flabs
    [InlineData("Test id : B165AAF4", "Test id : ")]
    [InlineData("Hasta No: 458712", "Hasta No: ")]
    [InlineData("Protokol No: 2023-0045", "Protokol No: ")]
    [InlineData("TC Kimlik No: 12345678901", "TC Kimlik No: ")]
    [InlineData("СНИЛС 123-456-789 01", "СНИЛС ")]
    public void Masks_identifiers_after_a_label(string text, string label)
    {
        Assert.StartsWith(label + PiiAnonymizer.IdMask, PiiAnonymizer.Anonymize(text));
    }

    [Fact]
    public void Masks_a_turkish_national_id_without_a_label()
    {
        Assert.Equal($"Kimlik: {PiiAnonymizer.IdMask}", PiiAnonymizer.Anonymize("Kimlik: 10000000146"));
    }

    [Theory]
    [InlineData("+91 7253928905")]      // Индия
    [InlineData("+34 928 45 12 34")]    // Испания
    [InlineData("+1 (415) 555-0132")]   // США
    [InlineData("+49 30 1234567")]      // Германия
    public void Masks_international_phone_numbers(string phone)
    {
        Assert.Equal($"Call {PiiAnonymizer.PhoneMask} now", PiiAnonymizer.Anonymize($"Call {phone} now"));
    }

    [Fact]
    public void Identifier_and_phone_rules_leave_lab_values_alone()
    {
        const string text = "Test Name Result Unit\nPlatelet Count 300000 150000 - 410000 /cumm\nSample 10 mL\nEGFR 78 >OR= 60 mL/min/1.73m2\nWBC 6.7 +/- 0.2";

        Assert.Equal(text, PiiAnonymizer.Anonymize(text));
    }

    [Fact]
    public void Fuzzy_name_matching_leaves_lab_values_alone()
    {
        const string text = "Glucose 5.4 mmol/L 3.9-5.6\nHemoglobin 135 g/L\nLeukocytes 6.1 10^9/L";

        Assert.Equal(text, PiiAnonymizer.Anonymize(text, ["Elif Kaya", "Ali Can"]));
    }
}
