namespace HealthlyApi.Dtos;

// POST /api/documents/{id}/analyze — результат ИИ-анализа документа (пока не сохраняется в БД)
public record DocumentAnalysisResponse(
    int DocumentId,
    List<AnalyzedIndicatorResponse> Indicators,
    int AbnormalCount,
    string Summary,                    // резюме ИИ простым языком
    List<string> Recommendations,
    List<string> SuggestedSpecialists, // врачи по показателям с отклонениями ("Endocrinologist"…)
    string AnonymizedText,             // текст, отправленный модели после маскирования персональных данных
    string Model,                      // "mock-ai-v1" — заглушка
    bool IsMock,
    DateTime AnalyzedAt,
    string Disclaimer);

public record AnalyzedIndicatorResponse(
    string TestName,
    string Value,
    string Unit,
    string ReferenceRange,  // из справочника норм, а если показателя там нет — из бланка
    bool? IsAbnormal,       // null — не удалось определить (нечисловое значение и нет нормы)
    string? Category,       // "Complete blood count", "Lipid profile"… (если показатель есть в справочнике)
    string? DoctorSpecialty,
    double Confidence);     // уверенность распознавания 0..1
