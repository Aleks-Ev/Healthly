using System.Globalization;
using System.Text.RegularExpressions;

namespace HealthlyApi.Services;

// Определяет, выходит ли значение показателя за референсный диапазон.
// Поддерживаемые форматы диапазона: "120-160", "120 – 160", "<5", "<=5", "≤5", ">1.2", ">=1.2", "≥1.2".
// После диапазона может идти текст — единицы или пометка, как печатают лаборатории: "7-25 mg/dL", "6-22 (calc)".
// Также понимаются ">OR= 60" (≥ 60, бланки Quest) и "Up to 15" (≤ 15).
// Возвращает null, если значение или диапазон не числовые (тогда флаг ставится вручную).
public static partial class ReferenceRangeEvaluator
{
    [GeneratedRegex(@"^\s*(-?\d+(?:[.,]\d+)?)\s*[-–—]\s*(-?\d+(?:[.,]\d+)?)(?:\s*[^\d\s.,].*)?\s*$")]
    private static partial Regex BetweenRegex();

    [GeneratedRegex(@"^\s*(<=|>=|≤|≥|<|>)\s*(-?\d+(?:[.,]\d+)?)(?:\s*[^\d\s.,].*)?\s*$")]
    private static partial Regex BoundRegex();

    // ">OR= 60", "> or = 60" → ">=60"
    [GeneratedRegex(@"([<>])\s*or\s*=", RegexOptions.IgnoreCase)]
    private static partial Regex OrEqualRegex();

    // "Up to 15", "Upto 15" → "<=15"
    [GeneratedRegex(@"^\s*up\s*to\s*", RegexOptions.IgnoreCase)]
    private static partial Regex UpToRegex();

    private static string Normalize(string range) =>
        UpToRegex().Replace(OrEqualRegex().Replace(range, "$1="), "<=");

    public static bool? IsAbnormal(string value, string? referenceRange)
    {
        if (string.IsNullOrWhiteSpace(referenceRange) || !TryParse(value, out var number))
        {
            return null;
        }

        referenceRange = Normalize(referenceRange);
        var between = BetweenRegex().Match(referenceRange);
        if (between.Success
            && TryParse(between.Groups[1].Value, out var min)
            && TryParse(between.Groups[2].Value, out var max))
        {
            return number < min || number > max;
        }

        var bound = BoundRegex().Match(referenceRange);
        if (bound.Success && TryParse(bound.Groups[2].Value, out var limit))
        {
            return bound.Groups[1].Value switch
            {
                "<" => number >= limit,
                "<=" or "≤" => number > limit,
                ">" => number <= limit,
                _ => number < limit, // ">=" или "≥"
            };
        }

        return null;
    }

    // Границы диапазона для графика: "120-160" → (120, 160), "<5" → (null, 5), ">1.2" → (1.2, null)
    public static (double? Min, double? Max) GetBounds(string? referenceRange)
    {
        if (string.IsNullOrWhiteSpace(referenceRange))
        {
            return (null, null);
        }

        referenceRange = Normalize(referenceRange);
        var between = BetweenRegex().Match(referenceRange);
        if (between.Success
            && TryParse(between.Groups[1].Value, out var min)
            && TryParse(between.Groups[2].Value, out var max))
        {
            return (min, max);
        }

        var bound = BoundRegex().Match(referenceRange);
        if (bound.Success && TryParse(bound.Groups[2].Value, out var limit))
        {
            return bound.Groups[1].Value is "<" or "<=" or "≤" ? (null, limit) : (limit, null);
        }

        return (null, null);
    }

    public static bool TryParse(string text, out double number) =>
        double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
}
