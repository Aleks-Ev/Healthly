namespace HealthlyApi.Services.Ai.Ocr;

// Распознавание текста на картинке. Сейчас — TesseractCliOcrEngine; другой движок (PaddleOCR, отдельный сервис)
// подключается заменой регистрации в Program.cs, DocumentTextExtractor не меняется.
public interface IOcrEngine
{
    // png — подготовленная картинка (ImagePreprocessor): серая, повёрнутая правильно, нужного размера
    Task<OcrResult> RecognizeAsync(byte[] png, CancellationToken cancellationToken);
}

// Confidence — средняя уверенность по словам, 0–100 (как у Tesseract); 0 — слов не найдено.
// UncertainWords — слова, прочитанные неуверенно; обычно это и есть ошибки OCR («7.1» → «7а», «10^9/L» → «1079/L»).
// Отдельным списком, а не пометкой в тексте: пометка внутри «+90 532…» сломала бы поиск телефона в PiiAnonymizer.
// Список никуда не отправляется — LlmDocumentProcessor по нему снижает confidence показателей.
public record OcrResult(string Text, double Confidence, IReadOnlyList<string> UncertainWords);
