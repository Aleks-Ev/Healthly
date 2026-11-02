namespace HealthlyApi.Models;

// Справочник референсных (нормальных) значений показателей анализов.
// Используется при ИИ-анализе документа: отклонение от нормы → категория и врач, к которому стоит обратиться.
//
// Источник данных — файл Data/reference-values.csv (его проверяют и правят врачи-консультанты, см. Data/REFERENCE_VALUES.md).
// При каждом запуске API файл синхронизируется в эту таблицу (ReferenceValuesCsv + DbInitializer), код читает нормы из БД.
// У одного показателя может быть несколько строк — для разных полов и возрастов.
public class ReferenceMedicalValue
{
    public const string AnySex = "Any";

    public int Id { get; set; }

    // Название показателя — так же, как TestResult.TestName ("Glucose"); сравнивается без учёта регистра
    public string MetricName { get; set; } = string.Empty;

    // Другие названия того же показателя через "|": "WBC|Total Leucocyte Count|Lökosit"
    public string Aliases { get; set; } = string.Empty;

    // Для кого строка: "Any", "Female", "Male"; возраст в полных годах, обе границы включительно, null — без ограничения
    public string Sex { get; set; } = AnySex;
    public int? AgeFrom { get; set; }
    public int? AgeTo { get; set; }

    // null — у диапазона нет нижней/верхней границы (например, холестерин: только «< 5.2»)
    public decimal? MinNormal { get; set; }
    public decimal? MaxNormal { get; set; }

    // Критические значения: опасно для жизни, нужен врач срочно (красная подсветка, уведомление). null — порога нет
    public decimal? CriticalLow { get; set; }
    public decimal? CriticalHigh { get; set; }

    public string Unit { get; set; } = string.Empty;            // "mmol/L", "g/L", "10^9/L"
    public string Category { get; set; } = string.Empty;        // группа анализов: "Complete blood count", "Lipid profile"…
    public string DoctorSpecialty { get; set; } = string.Empty; // к кому направить при отклонении: "Endocrinologist"…

    // Откуда взята норма и кто из врачей её проверил. Пустой ReviewedBy — черновик, ещё не проверен
    public string Source { get; set; } = string.Empty;
    public string ReviewedBy { get; set; } = string.Empty;
    public DateOnly? ReviewedAt { get; set; }

    public IEnumerable<string> AllNames() =>
        new[] { MetricName }.Concat(Aliases.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}
