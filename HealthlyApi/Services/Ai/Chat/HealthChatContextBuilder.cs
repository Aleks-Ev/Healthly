using HealthlyApi.Models;

namespace HealthlyApi.Services.Ai.Chat;

// Собирает контекст чата из данных базы. Чистая функция (без БД) — чтобы тестами проверить, что из профиля
// берутся только разрешённые поля: пол, возраст, рост, вес (+ группа крови, когда появится).
public static class HealthChatContextBuilder
{
    public const int MaxResults = 40;
    public const int MaxLaboratories = 40;
    public const int MaxDoctors = 40;

    // results — подтверждённые показатели пациента, от новых к старым (берётся последний по каждому названию)
    public static HealthChatContext Build(
        User patient,
        IEnumerable<TestResult> resultsNewestFirst,
        IEnumerable<Laboratory> laboratories,
        IEnumerable<User> doctors)
    {
        var profile = new ChatPatientProfile(
            Sex: string.IsNullOrWhiteSpace(patient.Gender) ? null : patient.Gender,
            // Только возраст в полных годах — сама дата рождения в модель не уходит
            Age: DocumentAnalysisService.GetAge(patient.DateOfBirth),
            HeightCm: patient.Height,
            WeightKg: patient.Weight,
            BloodType: null);

        var results = resultsNewestFirst
            .GroupBy(r => r.TestName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(MaxResults)
            .Select(r => new ChatLabResult(r.TestName.Trim(), r.Value.Trim(), r.Unit, r.ReferenceRange, r.IsAbnormal, r.IsCritical))
            .ToList();

        var labs = laboratories
            .Where(l => l.AcceptingPatients)
            .OrderBy(l => l.City).ThenBy(l => l.Name)
            .Take(MaxLaboratories)
            .Select(l => new ChatLaboratoryInfo(l.Name, l.City, l.Address, l.OpeningHours))
            .ToList();

        var doctorList = doctors
            .Where(d => d.Role == Roles.Doctor && d.AcceptingPatients)
            .OrderBy(d => d.City).ThenBy(d => d.FullName)
            .Take(MaxDoctors)
            .Select(d => new ChatDoctorInfo(d.FullName, d.Specialty, d.Workplace, d.City))
            .ToList();

        return new HealthChatContext(profile, results, labs, doctorList);
    }
}
