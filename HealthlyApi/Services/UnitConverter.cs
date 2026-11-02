namespace HealthlyApi.Services;

// Пересчёт значения из единиц лаборатории в единицы справочника норм.
// Без него «Hemoglobin 12 g/dL» сравнивался бы с нормой «120-160 g/L» и считался отклонением, хотя 12 g/dL = 120 g/L.
//
//   масса/объём:     g/dL ↔ g/L ↔ mg/dL ↔ mg/L
//   моль/объём:      mmol/L ↔ µmol/L; mg/dL → mmol/L — через молярную массу (известна для глюкозы и холестерина)
//   клетки/объём:    /µL = /cumm = /mm³ ↔ 10^3/µL = 10^9/L ↔ 10^6/µL = 10^12/L
// Неизвестная или искажённая OCR единица → null: тогда DocumentAnalysisService берёт диапазон, напечатанный в бланке.
public static class UnitConverter
{
    // Граммы в литре
    private static readonly Dictionary<string, double> MassPerLiter = new()
    {
        ["g/l"] = 1, ["g/dl"] = 10, ["mg/dl"] = 0.01, ["mg/l"] = 0.001,
    };

    // Моли в литре
    private static readonly Dictionary<string, double> MolesPerLiter = new()
    {
        ["mol/l"] = 1, ["mmol/l"] = 1e-3, ["umol/l"] = 1e-6,
    };

    // Клетки в литре
    private static readonly Dictionary<string, double> CellsPerLiter = new()
    {
        ["/ul"] = 1e6, ["10^3/ul"] = 1e9, ["10^9/l"] = 1e9, ["10^6/ul"] = 1e12, ["10^12/l"] = 1e12,
    };

    // Молярная масса, г/моль — для пересчёта mg/dL ↔ mmol/L
    private static readonly Dictionary<string, double> MolarMass = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Glucose"] = 180.16,
        ["Cholesterol"] = 386.65,
    };

    // Множитель: значение_в_справочнике = значение_в_бланке × множитель; null — пересчитать нельзя.
    // Пустая единица в бланке — считаем, что она совпадает со справочником (так было и до пересчёта)
    public static double? GetFactor(string metricName, string fromUnit, string toUnit)
    {
        var from = Normalize(fromUnit);
        var to = Normalize(toUnit);
        if (from.Length == 0 || from == to)
        {
            return 1;
        }

        if (Ratio(MassPerLiter, from, to) is { } mass)
        {
            return mass;
        }
        if (Ratio(MolesPerLiter, from, to) is { } moles)
        {
            return moles;
        }
        if (Ratio(CellsPerLiter, from, to) is { } cells)
        {
            return cells;
        }

        if (MolarMass.TryGetValue(metricName, out var molarMass))
        {
            // г/л → моль/л: делим на молярную массу
            if (MassPerLiter.TryGetValue(from, out var grams) && MolesPerLiter.TryGetValue(to, out var molesTo))
            {
                return grams / molarMass / molesTo;
            }
            if (MolesPerLiter.TryGetValue(from, out var molesFrom) && MassPerLiter.TryGetValue(to, out var gramsTo))
            {
                return molesFrom * molarMass / gramsTo;
            }
        }

        return null;
    }

    private static double? Ratio(Dictionary<string, double> units, string from, string to) =>
        units.TryGetValue(from, out var a) && units.TryGetValue(to, out var b) ? a / b : null;

    // "10^3/µL", "x10³/uL", "10 ^ 3 / ul" → "10^3/ul"; "/cumm", "cells/mm3" → "/ul"
    private static string Normalize(string unit)
    {
        var u = unit.Trim().ToLowerInvariant()
            .Replace(" ", "")
            .Replace('µ', 'u').Replace('μ', 'u')
            .Replace("×", "").Replace("x10", "10").Replace("*", "^")
            .Replace("³", "^3").Replace("⁶", "^6").Replace("⁹", "^9").Replace("¹²", "^12")
            .Replace("cells", "")
            .Replace("cumm", "ul").Replace("mm^3", "ul").Replace("mm3", "ul");
        return u switch
        {
            "ul" => "/ul",
            "thou/ul" or "k/ul" => "10^3/ul",
            "mil/ul" or "m/ul" or "million/ul" => "10^6/ul",
            _ => u,
        };
    }
}
