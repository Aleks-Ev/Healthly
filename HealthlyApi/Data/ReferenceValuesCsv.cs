using System.Globalization;
using System.Text;
using HealthlyApi.Models;

namespace HealthlyApi.Data;

// Чтение и проверка справочника норм из Data/reference-values.csv (формат и правила — Data/REFERENCE_VALUES.md).
//
// Файл правят врачи-консультанты, часто в Excel. Excel с турецкими/русскими региональными настройками пересохраняет
// CSV с разделителем ";" и десятичной запятой ("3,9") — поддерживается: разделитель определяется по строке заголовка.
//
// Ошибка в файле (минимум больше максимума, пересекающиеся строки, неизвестный пол…) — InvalidDataException с номером
// строки: API не запустится. Лучше остановиться, чем молча оценивать анализы по неверным нормам.
public static class ReferenceValuesCsv
{
    public static readonly string[] Columns =
    [
        "Metric", "Aliases", "Sex", "AgeFrom", "AgeTo", "Min", "Max", "CriticalLow", "CriticalHigh",
        "Unit", "Category", "Specialty", "Source", "ReviewedBy", "ReviewedAt",
    ];

    private static readonly string[] Sexes = [ReferenceMedicalValue.AnySex, Genders.Female, Genders.Male];

    public static List<ReferenceMedicalValue> Load(string path) => Parse(File.ReadAllText(path, Encoding.UTF8));

    public static List<ReferenceMedicalValue> Parse(string text)
    {
        var records = ReadRecords(text.TrimStart('﻿'));
        if (records.Count == 0)
        {
            throw new InvalidDataException("reference-values.csv is empty.");
        }

        var header = records[0].Fields.Select(f => f.Trim()).ToList();
        var missing = Columns.Where(c => !header.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
        if (missing.Count > 0)
        {
            throw new InvalidDataException($"reference-values.csv: missing columns: {string.Join(", ", missing)}.");
        }
        int Index(string column) => header.FindIndex(h => string.Equals(h, column, StringComparison.OrdinalIgnoreCase));

        var errors = new List<string>();
        var values = new List<(ReferenceMedicalValue Value, int Line)>();
        foreach (var (fields, line) in records.Skip(1))
        {
            if (fields.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }
            string Field(string column) => Index(column) < fields.Count ? fields[Index(column)].Trim() : string.Empty;
            void Error(string message) => errors.Add($"line {line}: {message}");

            decimal? Number(string column)
            {
                var raw = Field(column).Replace(',', '.');
                if (raw.Length == 0)
                {
                    return null;
                }
                if (decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    return number;
                }
                Error($"{column} \"{Field(column)}\" is not a number");
                return null;
            }

            int? Age(string column) => Number(column) is { } age ? (int)age : null;

            var value = new ReferenceMedicalValue
            {
                MetricName = Field("Metric"),
                Aliases = Field("Aliases"),
                Sex = Sexes.FirstOrDefault(s => string.Equals(s, Field("Sex"), StringComparison.OrdinalIgnoreCase)) ?? Field("Sex"),
                AgeFrom = Age("AgeFrom"),
                AgeTo = Age("AgeTo"),
                MinNormal = Number("Min"),
                MaxNormal = Number("Max"),
                CriticalLow = Number("CriticalLow"),
                CriticalHigh = Number("CriticalHigh"),
                Unit = Field("Unit"),
                Category = Field("Category"),
                DoctorSpecialty = Field("Specialty"),
                Source = Field("Source"),
                ReviewedBy = Field("ReviewedBy"),
            };
            if (Field("ReviewedAt").Length > 0)
            {
                if (DateOnly.TryParseExact(Field("ReviewedAt"), ["yyyy-MM-dd", "dd.MM.yyyy"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var reviewedAt))
                {
                    value.ReviewedAt = reviewedAt;
                }
                else
                {
                    Error($"ReviewedAt \"{Field("ReviewedAt")}\" must be a date like 2026-11-02");
                }
            }

            foreach (var error in Validate(value))
            {
                Error(error);
            }
            values.Add((value, line));
        }

        errors.AddRange(FindOverlaps(values));
        if (errors.Count > 0)
        {
            throw new InvalidDataException("reference-values.csv has errors:\n" + string.Join("\n", errors));
        }
        return values.Select(v => v.Value).ToList();
    }

    private static IEnumerable<string> Validate(ReferenceMedicalValue value)
    {
        if (value.MetricName.Length == 0)
        {
            yield return "Metric is empty";
        }
        if (!Sexes.Contains(value.Sex))
        {
            yield return $"Sex \"{value.Sex}\" must be Any, Female or Male";
        }
        if (value.Unit.Length == 0 || value.Category.Length == 0 || value.DoctorSpecialty.Length == 0)
        {
            yield return "Unit, Category and Specialty are required";
        }
        if (value.MinNormal is null && value.MaxNormal is null)
        {
            yield return "at least one of Min and Max is required";
        }
        if (value.MinNormal > value.MaxNormal)
        {
            yield return FormattableString.Invariant($"Min {value.MinNormal} is greater than Max {value.MaxNormal}");
        }
        if (value.AgeFrom > value.AgeTo)
        {
            yield return $"AgeFrom {value.AgeFrom} is greater than AgeTo {value.AgeTo}";
        }
        // Критическое значение лежит за пределами нормы, иначе в норме оказалось бы «опасно для жизни»
        if (value.CriticalLow is { } low && low >= (value.MinNormal ?? value.MaxNormal))
        {
            yield return FormattableString.Invariant($"CriticalLow {low} must be below the normal range");
        }
        if (value.CriticalHigh is { } high && high <= (value.MaxNormal ?? value.MinNormal))
        {
            yield return FormattableString.Invariant($"CriticalHigh {high} must be above the normal range");
        }
    }

    // Для одного человека должна подходить не больше одной строки показателя: пересечение по полу и возрасту — ошибка.
    // Одинаковые названия/синонимы у разных показателей — тоже ошибка (непонятно, какую норму брать)
    private static IEnumerable<string> FindOverlaps(List<(ReferenceMedicalValue Value, int Line)> values)
    {
        for (var i = 0; i < values.Count; i++)
        {
            for (var j = i + 1; j < values.Count; j++)
            {
                var (a, lineA) = values[i];
                var (b, lineB) = values[j];
                var sameMetric = string.Equals(a.MetricName, b.MetricName, StringComparison.OrdinalIgnoreCase);
                if (!sameMetric)
                {
                    var shared = a.AllNames().Intersect(b.AllNames(), StringComparer.OrdinalIgnoreCase).FirstOrDefault();
                    if (shared != null)
                    {
                        yield return $"lines {lineA} and {lineB}: name \"{shared}\" is used by two metrics ({a.MetricName}, {b.MetricName})";
                    }
                    continue;
                }

                if (!string.Equals(a.Unit, b.Unit, StringComparison.OrdinalIgnoreCase))
                {
                    yield return $"lines {lineA} and {lineB}: {a.MetricName} must use one unit in all rows ({a.Unit} / {b.Unit})";
                }
                var sexesOverlap = a.Sex == ReferenceMedicalValue.AnySex || b.Sex == ReferenceMedicalValue.AnySex || a.Sex == b.Sex;
                var agesOverlap = (a.AgeFrom ?? 0) <= (b.AgeTo ?? int.MaxValue) && (b.AgeFrom ?? 0) <= (a.AgeTo ?? int.MaxValue);
                if (sexesOverlap && agesOverlap)
                {
                    yield return $"lines {lineA} and {lineB}: two {a.MetricName} rows apply to the same sex and age";
                }
            }
        }
    }

    // Записи CSV по RFC 4180: поля в кавычках могут содержать разделитель и "" (экранированная кавычка)
    private static List<(List<string> Fields, int Line)> ReadRecords(string text)
    {
        var firstLine = text.Split('\n', 2)[0];
        var delimiter = firstLine.Count(c => c == ';') > firstLine.Count(c => c == ',') ? ';' : ',';

        var records = new List<(List<string>, int)>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var recordLine = 1;

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    if (c == '\n')
                    {
                        line++;
                    }
                    field.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else if (c == '\n')
            {
                fields.Add(field.ToString().TrimEnd('\r'));
                records.Add((fields, recordLine));
                fields = [];
                field.Clear();
                line++;
                recordLine = line;
            }
            else
            {
                field.Append(c);
            }
        }
        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString().TrimEnd('\r'));
            records.Add((fields, recordLine));
        }
        return records;
    }
}
