namespace HealthlyApi.Services;

// Пересчёт значения из единиц лаборатории в единицы справочника норм.
// Без него «Hemoglobin 12 g/dL» сравнивался бы с нормой «120-160 g/L» и считался отклонением, хотя 12 g/dL = 120 g/L.
//
//   масса/объём:     g/L, g/dL, mg/dL, mg/L, µg/dL, µg/L, ng/mL, ng/dL, pg/mL
//   моль/объём:      mol/L, mmol/L, µmol/L, nmol/L, pmol/L; масса ↔ моль — через молярную массу показателя (MolarMass)
//   клетки/объём:    /µL = /cumm = /mm³ ↔ 10^3/µL = 10^9/L ↔ 10^6/µL = 10^12/L; лакх/µL (индийские бланки) = 10^5/µL
//   доли:            % ↔ L/L (гематокрит)
//   синонимы:        µIU/mL = mIU/L, IU/L = U/L, mm/hr = mm/h, mEq/L = mmol/L (только одновалентные ионы)
// Неизвестная или искажённая OCR единица → null: тогда DocumentAnalysisService берёт диапазон, напечатанный в бланке.
public static class UnitConverter
{
    // Граммы в литре
    private static readonly Dictionary<string, double> MassPerLiter = new()
    {
        ["g/l"] = 1, ["g/dl"] = 10, ["mg/dl"] = 1e-2, ["mg/l"] = 1e-3, ["ug/dl"] = 1e-5, ["ug/l"] = 1e-6,
        ["ng/ml"] = 1e-6, ["ng/dl"] = 1e-8, ["pg/ml"] = 1e-9,
    };

    // Моли в литре
    private static readonly Dictionary<string, double> MolesPerLiter = new()
    {
        ["mol/l"] = 1, ["mmol/l"] = 1e-3, ["umol/l"] = 1e-6, ["nmol/l"] = 1e-9, ["pmol/l"] = 1e-12,
    };

    // Клетки в литре
    private static readonly Dictionary<string, double> CellsPerLiter = new()
    {
        ["/ul"] = 1e6, ["10^3/ul"] = 1e9, ["10^9/l"] = 1e9, ["10^6/ul"] = 1e12, ["10^12/l"] = 1e12, ["lakh/ul"] = 1e11,
    };

    // Доля единицы
    private static readonly Dictionary<string, double> Fractions = new()
    {
        ["%"] = 1e-2, ["l/l"] = 1,
    };

    // Молярная масса, г/моль — для пересчёта массы в моли (mg/dL ↔ mmol/L и т. п.). Ключ — MetricName справочника
    private static readonly Dictionary<string, double> MolarMass = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Glucose"] = 180.16,
        ["Cholesterol"] = 386.65,
        ["LDL cholesterol"] = 386.65,
        ["HDL cholesterol"] = 386.65,
        ["Triglycerides"] = 885.7,
        ["Urea"] = 60.06,
        ["Creatinine"] = 113.12,
        ["Uric acid"] = 168.11,
        ["Calcium"] = 40.08,
        ["Total bilirubin"] = 584.66,
        ["Iron"] = 55.845,
        ["Vitamin B12"] = 1355.37,
        ["Vitamin D"] = 400.64,
        ["Free T4"] = 776.87,
    };

    // mEq/L = mmol/L только для ионов с зарядом ±1 (у кальция 1 mEq = 0.5 mmol — для него пересчёт не делаем)
    private static readonly HashSet<string> MonovalentIons = new(StringComparer.OrdinalIgnoreCase)
    {
        "Sodium", "Potassium", "Chloride", "Bicarbonate",
    };

    // Множитель: значение_в_справочнике = значение_в_бланке × множитель; null — пересчитать нельзя.
    // Пустая единица в бланке — считаем, что она совпадает со справочником (так было и до пересчёта)
    public static double? GetFactor(string metricName, string fromUnit, string toUnit)
    {
        var from = Normalize(fromUnit, metricName);
        var to = Normalize(toUnit, metricName);
        if (from.Length == 0 || from == to)
        {
            return 1;
        }

        foreach (var units in new[] { MassPerLiter, MolesPerLiter, CellsPerLiter, Fractions })
        {
            if (units.TryGetValue(from, out var a) && units.TryGetValue(to, out var b))
            {
                return a / b;
            }
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

    // "10^3/µL", "x10³/uL", "10 ^ 3 / ul" → "10^3/ul"; "/cumm", "cells/mm3" → "/ul"; "µIU/mL" → "miu/l"
    private static string Normalize(string unit, string metricName)
    {
        var u = unit.Trim().ToLowerInvariant()
            .Replace(" ", "")
            .Replace('µ', 'u').Replace('μ', 'u')
            .Replace("×", "").Replace("x10", "10").Replace("*", "^")
            .Replace("³", "^3").Replace("⁶", "^6").Replace("⁹", "^9").Replace("¹²", "^12").Replace("²", "2")
            .Replace("cells", "")
            .Replace("cumm", "ul").Replace("mm^3", "ul").Replace("mm3", "ul");
        u = u switch
        {
            "ul" => "/ul",
            "thou/ul" or "k/ul" => "10^3/ul",
            "mil/ul" or "m/ul" or "million/ul" => "10^6/ul",
            "lakh/ul" or "lakhs/ul" => "lakh/ul",
            "uiu/ml" or "mu/l" => "miu/l",
            "iu/l" => "u/l",
            "mm/hr" or "mm/1h" or "mm/saat" => "mm/h",
            "ml/min/1.73m^2" => "ml/min/1.73m2",
            _ => u,
        };
        return u == "meq/l" && MonovalentIons.Contains(metricName) ? "mmol/l" : u;
    }
}
