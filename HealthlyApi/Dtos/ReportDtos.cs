namespace HealthlyApi.Dtos;

// GET /api/users/{id}/report — сводный медицинский отчёт пользователя
public record UserReportResponse(
    UserResponse User,
    int DocumentsCount,
    int ResultsCount,
    int AbnormalCount,
    DateTime? LastUploadDate,
    List<ReportIndicator> Indicators);

// Последнее значение одного показателя (например, "Hemoglobin") по всем документам
public record ReportIndicator(
    string TestName,
    string LatestValue,
    string Unit,
    string ReferenceRange,
    bool IsAbnormal,
    DateTime MeasuredAt,
    int MeasurementsCount,
    int DocumentId);

// GET /api/users/{id}/trends — история числовых значений одного показателя для графика
public record IndicatorTrend(
    string TestName,
    string Unit,
    string ReferenceRange,
    double? ReferenceMin, // null — нижней границы нет (или диапазон не числовой)
    double? ReferenceMax,
    List<TrendPoint> Points);

// Одно измерение: дата документа и значение
public record TrendPoint(
    int DocumentId,
    DateTime MeasuredAt,
    double Value,
    bool IsAbnormal);
