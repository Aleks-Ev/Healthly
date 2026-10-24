using System.Text.RegularExpressions;

namespace HealthlyApi.Services;

// Маскирует персональные данные (PII) в сыром тексте документа перед отправкой во внешний ИИ (LLM):
// модели нужны показатели анализов, а не имя, телефон и e-mail пациента.
//
// Порядок важен: сначала e-mail (в нём бывают цифры, похожие на телефон), затем телефоны, даты и имена.
//   e-mail                      → [REDACTED_EMAIL]
//   телефоны +90 / 0 5xx (Турция), +7 / 8 (Россия)   → [REDACTED_PHONE]
//   даты 18.04.1992, 18/04/92, 1992-04-18           → [REDACTED_DATE]
//   имена                       → [REDACTED_NAME]:
//     1) значение после метки: "Patient: Elif Kaya", "ФИО: Иванов Иван", "Hasta Adı: Ayşe Yılmaz";
//     2) имя после звания ("Dr. Ayse Demir", "Uzm. Dr. Ali Kaya", "д-р Иванов");
//        русское ФИО с отчеством ("Иванов Иван Петрович") и фамилия с инициалами ("Иванов И.И.", "И. И. Иванов");
//     3) имена, известные вызывающему коду (например, ФИО пациента из базы), — надёжнее всего.
//
// Регулярные выражения не находят все имена в свободном тексте (например, просто «Elif пришла на приём»),
// поэтому для реальных данных передавайте knownNames.
public static partial class PiiAnonymizer
{
    public const string EmailMask = "[REDACTED_EMAIL]";
    public const string PhoneMask = "[REDACTED_PHONE]";
    public const string DateMask = "[REDACTED_DATE]";
    public const string NameMask = "[REDACTED_NAME]";

    // Ограничение времени на одно выражение — защита от зависания на очень длинном/«злом» тексте (ReDoS)
    private const int TimeoutMs = 1000;

    public static string Anonymize(string text, IEnumerable<string>? knownNames = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var result = EmailRegex().Replace(text, EmailMask);
        result = PhoneRegex().Replace(result, PhoneMask);
        result = DateRegex().Replace(result, DateMask);
        result = IsoDateRegex().Replace(result, DateMask);

        // Сначала известные имена целиком («Elif Kaya»), потом по частям («Kaya» отдельно)
        foreach (var name in ExpandKnownNames(knownNames))
        {
            result = Regex.Replace(result, $@"(?<!\p{{L}}){Regex.Escape(name)}(?!\p{{L}})", NameMask,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(TimeoutMs));
        }

        result = LabeledNameRegex().Replace(result, match => match.Groups["label"].Value + NameMask);
        result = TitledNameRegex().Replace(result, match => match.Groups["title"].Value + NameMask);
        result = RussianFullNameRegex().Replace(result, NameMask);
        result = SurnameWithInitialsRegex().Replace(result, NameMask);
        result = InitialsWithSurnameRegex().Replace(result, NameMask);

        return result;
    }

    // "Elif Kaya" → ["Elif Kaya", "Elif", "Kaya"] (части короче 3 букв пропускаем — слишком много ложных совпадений)
    private static IEnumerable<string> ExpandKnownNames(IEnumerable<string>? knownNames) =>
        (knownNames ?? [])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .SelectMany(name => new[] { name.Trim() }.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .Where(part => part.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(part => part.Length);

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.None, TimeoutMs)]
    private static partial Regex EmailRegex();

    // +90 532 123 45 67, 0090..., 0 (532) 123-45-67 — Турция; +7 (912) 345-67-89, 8 912 345 67 89 — Россия.
    // Разделители между группами цифр — пробелы, дефисы, скобки
    [GeneratedRegex(
        @"(?<![\w+])(?:(?:\+|00)90|\+7|8|0)[\s\-()]*\d{3}[\s\-()]*\d{3}[\s\-]*\d{2}[\s\-]*\d{2}(?!\d)",
        RegexOptions.None, TimeoutMs)]
    private static partial Regex PhoneRegex();

    // ДД.ММ.ГГГГ / ДД/ММ/ГГ / ДД-ММ-ГГГГ. Оба разделителя одинаковые (\k<sep>) и день/месяц в допустимых пределах —
    // иначе под «дату» попали бы референсные диапазоны вроде "12.5-15.5"
    [GeneratedRegex(
        @"(?<![\d.])(?:0?[1-9]|[12]\d|3[01])(?<sep>[./\-])(?:0?[1-9]|1[0-2])\k<sep>(?:(?:19|20)\d{2}|\d{2})(?![\d.]\d)",
        RegexOptions.None, TimeoutMs)]
    private static partial Regex DateRegex();

    // ГГГГ-ММ-ДД (ISO)
    [GeneratedRegex(@"(?<!\d)(?:19|20)\d{2}-(?:0[1-9]|1[0-2])-(?:0[1-9]|[12]\d|3[01])(?!\d)", RegexOptions.None, TimeoutMs)]
    private static partial Regex IsoDateRegex();

    // Метка + двоеточие + от 1 до 4 слов с заглавной буквы (латиница, кириллица, турецкие буквы).
    // Без учёта регистра — только метка ((?i:...)), а слова имени разделены лишь пробелами/табами:
    // иначе \s захватил бы перевод строки и «съел» название показателя со следующей строки
    [GeneratedRegex(
        @"(?<label>\b(?i:Patient(?:[ \t]+name)?|Full[ \t]+name|Name|Doctor|Physician|Пациент(?:ка)?|ФИО|Ф\.[ \t]?И\.[ \t]?О\.|Врач|Hasta(?:[ \t]+Ad[ıi](?:[ \t]+Soyad[ıi])?)?|Ad[ıi][ \t]+Soyad[ıi]|Ad[ \t]+Soyad|Doktor)[ \t]*:[ \t]*)" +
        @"(?:Dr\.[ \t]*)?\p{Lu}[\p{L}'\-]*(?:[ \t]+\p{Lu}[\p{L}'\-]*\.?){0,3}",
        RegexOptions.CultureInvariant, TimeoutMs)]
    private static partial Regex LabeledNameRegex();

    // Имя после звания: «Dr. Ayse Demir», «Prof. Mehmet Öz», «Uzm. Dr. Ali Kaya», «д-р Иванов» — звание остаётся
    [GeneratedRegex(
        @"(?<title>(?:\b(?:Dr|Prof|Doç|Uzm)\.?|\bDoctor|\bДоктор|\bд-р|\bпроф\.)[ \t]+(?:Dr\.?[ \t]+)?)" +
        @"\p{Lu}[\p{L}'\-]*(?:[ \t]+\p{Lu}[\p{L}'\-]*){0,2}",
        RegexOptions.CultureInvariant, TimeoutMs)]
    private static partial Regex TitledNameRegex();

    // «Иванов Иван Петрович», «Петрова Анна Сергеевна» — по окончанию отчества
    [GeneratedRegex(
        @"\b\p{Lu}\p{Ll}+[ \t]+\p{Lu}\p{Ll}+[ \t]+\p{Lu}\p{Ll}+(?:ович|евич|ич|овна|евна|ична|инична)\b",
        RegexOptions.CultureInvariant, TimeoutMs)]
    private static partial Regex RussianFullNameRegex();

    // «Иванов И.И.», «Ivanov I. P.»
    [GeneratedRegex(@"\b\p{Lu}\p{Ll}+[ \t]+\p{Lu}\.[ \t]?(?:\p{Lu}\.)?", RegexOptions.CultureInvariant, TimeoutMs)]
    private static partial Regex SurnameWithInitialsRegex();

    // «И.И. Иванов», «I. Ivanov»
    [GeneratedRegex(@"\b\p{Lu}\.[ \t]?(?:\p{Lu}\.[ \t]?)?\p{Lu}\p{Ll}+", RegexOptions.CultureInvariant, TimeoutMs)]
    private static partial Regex InitialsWithSurnameRegex();
}
