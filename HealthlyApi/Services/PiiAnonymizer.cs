using System.Text.RegularExpressions;

namespace HealthlyApi.Services;

// Маскирует персональные данные (PII) в сыром тексте документа перед отправкой во внешний ИИ (LLM):
// модели нужны показатели анализов, а не имя, телефон и e-mail пациента.
//
// Порядок важен: сначала e-mail (в нём бывают цифры, похожие на телефон), затем идентификаторы, телефоны, даты и имена.
//   e-mail                      → [REDACTED_EMAIL]
//   идентификаторы после метки: "Patient ID: PAC001", "Hasta No: 123", "TC Kimlik No: …", "Report ID : RE1"
//     (и их искажения OCR: "Patient 10", "Test 1d"); 11-значный номер TC Kimlik без метки → [REDACTED_ID]
//   телефоны +90 / 0 5xx (Турция), +7 / 8 (Россия), любой международный "+код страны"   → [REDACTED_PHONE]
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
    public const string IdMask = "[REDACTED_ID]";

    // Ограничение времени на одно выражение — защита от зависания на очень длинном/«злом» тексте (ReDoS)
    private const int TimeoutMs = 1000;

    public static string Anonymize(string text, IEnumerable<string>? knownNames = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var result = EmailRegex().Replace(text, EmailMask);
        result = LabeledIdRegex().Replace(result, match => match.Groups["label"].Value + IdMask);
        result = PhoneRegex().Replace(result, PhoneMask);
        result = InternationalPhoneRegex().Replace(result, PhoneMask);
        result = TurkishNationalIdRegex().Replace(result, IdMask);
        result = DateRegex().Replace(result, DateMask);
        result = IsoDateRegex().Replace(result, DateMask);

        // Сначала известные имена целиком («Elif Kaya»), потом по частям («Kaya» отдельно)
        foreach (var name in ExpandKnownNames(knownNames))
        {
            result = Regex.Replace(result, $@"(?<!\p{{L}}){Regex.Escape(name)}(?!\p{{L}})", NameMask,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(TimeoutMs));
        }

        result = MaskOcrVariantsOfKnownNames(result, knownNames);

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

    // Текст с фото (OCR) содержит ошибки: «E1if Kaya», «Ayse Yilmaz» вместо «Ayşe Yılmaz», «Kaγa». Точное сравнение
    // такое имя пропустит, поэтому каждое слово сравниваем с частями известных имён «с допуском»:
    //   - похожие для OCR символы приводим к одному (1/l/i/ı/| → l, 0 → o, 5 → s), убираем диакритику (ş → s);
    //   - разрешаем 1 ошибку (замена/вставка/удаление буквы) для частей от 4 букв и 2 — от 8 букв.
    // Короткие части (Ali, Can) сравниваются только точно — иначе маскировались бы обычные слова.
    private const int FuzzyMinLength = 4;

    private static string MaskOcrVariantsOfKnownNames(string text, IEnumerable<string>? knownNames)
    {
        var parts = ExpandKnownNames(knownNames)
            .Where(part => !part.Contains(' ') && part.Length >= FuzzyMinLength)
            .Select(NormalizeForOcr)
            .Distinct()
            .ToList();
        if (parts.Count == 0)
        {
            return text;
        }

        return WordRegex().Replace(text, match =>
        {
            var word = match.Value;
            if (!word.Any(char.IsLetter) || word.Length < FuzzyMinLength - 1)
            {
                return word;
            }
            var normalized = NormalizeForOcr(word);
            return parts.Any(part => Levenshtein(normalized, part, part.Length >= 8 ? 2 : 1)) ? NameMask : word;
        });
    }

    private static string NormalizeForOcr(string word)
    {
        // Две буквы, которые OCR склеивает в одну и наоборот: «rn» ↔ «m», «vv» ↔ «w»
        var lower = word.ToLowerInvariant().Replace("rn", "m").Replace("vv", "w");
        var builder = new System.Text.StringBuilder(word.Length);
        foreach (var c in lower.Normalize(System.Text.NormalizationForm.FormD))
        {
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            builder.Append(char.ToLowerInvariant(c) switch
            {
                '1' or 'i' or 'ı' or '|' or '!' => 'l',
                '0' => 'o',
                '5' => 's',
                var other => other,
            });
        }
        return builder.ToString();
    }

    // true — расстояние Левенштейна между словами не больше maxDistance
    private static bool Levenshtein(string a, string b, int maxDistance)
    {
        if (Math.Abs(a.Length - b.Length) > maxDistance)
        {
            return false;
        }
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        var current = new int[b.Length + 1];
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + cost);
            }
            (previous, current) = (current, previous);
        }
        return previous[b.Length] <= maxDistance;
    }

    // Слово для нечёткого сравнения: буквы, цифры и похожие на буквы символы OCR (| и !)
    [GeneratedRegex(@"[\p{L}\p{N}|!]+", RegexOptions.None, TimeoutMs)]
    private static partial Regex WordRegex();

    // Метка идентификатора + значение с хотя бы одной цифрой. "ID" бывает распознан OCR как "1D", "I0", "10", "I1D", "lD"
    [GeneratedRegex(
        @"(?<label>\b(?i:(?:Patient|Hasta|Report|Rapor|Sample|Numune|Specimen|Test|Lab|Barcode|Barkod|Protocol|Protokol|Accession|Пациент|Карта)[ \t]*(?:[Il1|]{1,2}[D0O]|No|Number|Numaras[ıi]|Kodu|№|#)\.?|MRN|T\.?C\.?[ \t]*(?:Kimlik[ \t]*)?No\.?|Kimlik[ \t]*No\.?|СНИЛС|Полис(?:[ \t]*ОМС)?)[ \t]*[:#№]?[ \t]*)" +
        @"(?=[\p{L}\p{N}\-/]*\p{N})[\p{L}\p{N}][\p{L}\p{N}\-/]*",
        RegexOptions.CultureInvariant, TimeoutMs)]
    private static partial Regex LabeledIdRegex();

    // Международный номер: "+" и код страны, всего от 8 до 15 цифр (E.164), разделители — пробелы, дефисы, скобки
    [GeneratedRegex(@"(?<![\w+])\+[1-9](?:[\s\-()]*\d){7,14}(?!\d)", RegexOptions.None, TimeoutMs)]
    private static partial Regex InternationalPhoneRegex();

    // TC Kimlik No — 11 цифр, первая не 0; в анализах таких «голых» 11-значных чисел не бывает
    [GeneratedRegex(@"(?<![\d.,])[1-9]\d{10}(?![\d.,])", RegexOptions.None, TimeoutMs)]
    private static partial Regex TurkishNationalIdRegex();

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
