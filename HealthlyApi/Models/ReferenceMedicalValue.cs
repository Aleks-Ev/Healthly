namespace HealthlyApi.Models;

// Справочник референсных (нормальных) значений показателей анализов.
// Используется при ИИ-анализе документа: отклонение от нормы → категория и врач, к которому стоит обратиться.
// Нормы — общие для взрослых; у конкретной лаборатории диапазон в бланке может немного отличаться.
public class ReferenceMedicalValue
{
    public int Id { get; set; }

    // Название показателя — так же, как TestResult.TestName ("Glucose"); сравнивается без учёта регистра
    public string MetricName { get; set; } = string.Empty;

    // null — у диапазона нет нижней/верхней границы (например, холестерин: только «< 5.2»)
    public decimal? MinNormal { get; set; }
    public decimal? MaxNormal { get; set; }

    public string Unit { get; set; } = string.Empty;            // "mmol/L", "g/L", "10^9/L"
    public string Category { get; set; } = string.Empty;        // группа анализов: "Complete blood count", "Lipid profile"…
    public string DoctorSpecialty { get; set; } = string.Empty; // к кому направить при отклонении: "Endocrinologist"…
}
