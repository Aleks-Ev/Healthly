using HealthlyApi.Services.Ai;

namespace HealthlyApi.Models;

// Черновик ИИ-анализа документа: «ИИ предлагает — человек подтверждает».
//   1. POST /analyze — ИИ читает бланк, результат сохраняется сюда (повторное открытие — без нового вызова ИИ);
//   2. человек сверяет показатели с бланком, правит и подтверждает (POST /analysis/confirm);
//   3. подтверждённые значения записываются в TestResults — только они попадают в историю и графики.
// Хранятся сырые показатели от ИИ, а не оценка: «в норме / нет» пересчитывается при каждом чтении по текущему
// справочнику норм — если врачи поправят норму, черновик сразу покажет новую оценку.
// Один анализ на документ; повторный анализ (force) заменяет черновик и снимает подтверждение.
public class DocumentAnalysis
{
    public int Id { get; set; }

    public int MedicalDocumentId { get; set; }
    public MedicalDocument? MedicalDocument { get; set; }

    public List<AiExtractedIndicator> Indicators { get; set; } = [];   // jsonb
    public string Summary { get; set; } = string.Empty;
    public List<string> Recommendations { get; set; } = [];            // text[]
    public string AnonymizedText { get; set; } = string.Empty;         // что ушло в модель (уже без персональных данных)
    public string Model { get; set; } = string.Empty;                  // "gemini:gemini-3.5-flash", "mock-ai-v1"
    public bool IsMock { get; set; }
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;

    // null — черновик ещё не подтверждён
    public DateTime? ConfirmedAt { get; set; }
    public int? ConfirmedByUserId { get; set; }
}
