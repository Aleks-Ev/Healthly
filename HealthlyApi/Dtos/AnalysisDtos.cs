namespace HealthlyApi.Dtos;

// POST /api/documents/{id}/analyze — результат ИИ-анализа документа (пока не сохраняется в БД)
public record DocumentAnalysisResponse(
    int DocumentId,
    List<AnalyzedIndicatorResponse> Indicators,
    int AbnormalCount,
    int CriticalCount,                 // > 0 — показать красное предупреждение «срочно к врачу»
    string Summary,                    // резюме ИИ простым языком
    List<string> Recommendations,
    List<string> SuggestedSpecialists, // врачи по показателям с отклонениями, сначала — по критическим ("Endocrinologist"…)
    string AnonymizedText,             // текст, отправленный модели после маскирования персональных данных
    string Model,                      // "mock-ai-v1" — заглушка
    bool IsMock,
    DateTime AnalyzedAt,
    string Disclaimer);

public record AnalyzedIndicatorResponse(
    string TestName,
    string Value,
    string Unit,
    string ReferenceRange,  // из справочника норм (с учётом пола и возраста, в единицах бланка), а если нет — из бланка
    bool? IsAbnormal,       // null — не удалось определить или значение похоже на ошибку OCR (тогда Confidence ≤ 0.4)
    bool IsCritical,        // за критическим порогом справочника: опасно, нужен врач срочно (всегда и IsAbnormal = true)
    string? Category,       // "Complete blood count", "Lipid profile"… (если показатель есть в справочнике)
    string? DoctorSpecialty,
    double Confidence);     // уверенность распознавания 0..1
