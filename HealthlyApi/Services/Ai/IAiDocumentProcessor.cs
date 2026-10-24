using HealthlyApi.Models;

namespace HealthlyApi.Services.Ai;

// Контракт ИИ-обработки медицинского документа: файл → распознанный текст (OCR) → обезличивание (PiiAnonymizer)
// → извлечение показателей и резюме (LLM).
//
// Сейчас в DI зарегистрирована заглушка MockAiDocumentProcessor. Реальная реализация (например,
// OcrLlmDocumentProcessor: Tesseract/облачный OCR + Claude/другая LLM) должна реализовать этот же интерфейс —
// контроллер, DocumentAnalysisService и формат ответа API при замене не меняются (см. Program.cs).
//
// Правила для реализаций:
// - во внешний сервис отправляется только текст ПОСЛЕ PiiAnonymizer.Anonymize (его же вернуть в AnonymizedText);
// - решать «в норме / не в норме» не нужно — это делает DocumentAnalysisService по справочнику норм из БД;
//   нормы переданы во входных данных, чтобы LLM могла учесть их в резюме.
public interface IAiDocumentProcessor
{
    Task<AiDocumentAnalysis> AnalyzeAsync(AiDocumentInput input, CancellationToken cancellationToken);
}

// Что получает процессор
public record AiDocumentInput(
    int DocumentId,
    string FileName,
    string ContentType,          // "application/pdf", "image/png", "image/jpeg"
    Stream Content,              // содержимое файла; закрывает вызывающий код
    AiPatientContext Patient,
    IReadOnlyList<ReferenceMedicalValue> ReferenceValues);

// Контекст пациента: возраст и пол влияют на интерпретацию, имя нужно только для обезличивания (в LLM не уходит)
public record AiPatientContext(string FullName, string Gender, int? Age);

// Что возвращает процессор
public record AiDocumentAnalysis(
    IReadOnlyList<AiExtractedIndicator> Indicators,
    string Summary,                       // резюме на понятном пациенту языке (английский — как весь интерфейс)
    IReadOnlyList<string> Recommendations,
    string AnonymizedText,                // текст, который ушёл в модель (уже без персональных данных)
    string Model,                         // идентификатор модели/движка, например "mock-ai-v1"
    bool IsMock);

// Один показатель, найденный в документе
public record AiExtractedIndicator(
    string TestName,       // название как в справочнике норм: "Glucose", "Hemoglobin"…
    string Value,          // строкой, как в TestResult ("5.4"; бывает и "positive")
    string Unit,
    string ReferenceRange, // диапазон, напечатанный в бланке лаборатории (может быть пустым)
    double Confidence);    // уверенность распознавания 0..1
