using HealthlyApi.Models;

namespace HealthlyApi.Data;

// Базовые нормы общего и биохимического анализа крови для взрослых.
// Попадают в базу через миграцию (HasData в AppDbContext), поэтому есть в любой среде, включая Production.
// Чтобы изменить или добавить норму: правка здесь → dotnet ef migrations add <Name> → dotnet ef database update.
public static class ReferenceMedicalValueSeed
{
    public static readonly ReferenceMedicalValue[] Values =
    [
        // Глюкоза натощак (венозная плазма)
        new()
        {
            Id = 1,
            MetricName = "Glucose",
            MinNormal = 3.9m,
            MaxNormal = 5.6m,
            Unit = "mmol/L",
            Category = "Biochemistry",
            DoctorSpecialty = "Endocrinologist",
        },
        // Общий холестерин: нормой считается только верхняя граница (желательный уровень < 5.2)
        new()
        {
            Id = 2,
            MetricName = "Cholesterol",
            MinNormal = null,
            MaxNormal = 5.2m,
            Unit = "mmol/L",
            Category = "Lipid profile",
            DoctorSpecialty = "Cardiologist",
        },
        // Гемоглобин: общий диапазон для взрослых (у женщин обычно 120–150, у мужчин 130–170)
        new()
        {
            Id = 3,
            MetricName = "Hemoglobin",
            MinNormal = 120m,
            MaxNormal = 160m,
            Unit = "g/L",
            Category = "Complete blood count",
            DoctorSpecialty = "Hematologist",
        },
        // Лейкоциты (WBC)
        new()
        {
            Id = 4,
            MetricName = "Leukocytes",
            MinNormal = 4.0m,
            MaxNormal = 9.0m,
            Unit = "10^9/L",
            Category = "Complete blood count",
            DoctorSpecialty = "Hematologist",
        },
    ];
}
