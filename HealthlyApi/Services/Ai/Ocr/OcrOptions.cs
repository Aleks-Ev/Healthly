namespace HealthlyApi.Services.Ai.Ocr;

// Настройки локального OCR (секция "Ocr" в appsettings.json). Распознавание идёт программой Tesseract на нашем сервере:
// фото бланка с именем пациента никуда не отправляется.
//   Docker: Tesseract и языки ставятся в образ (HealthlyApi/Dockerfile), настраивать ничего не нужно.
//   Windows: установить Tesseract (winget install UB-Mannheim.TesseractOCR), скачать rus/tur.traineddata и прописать пути
//   в user-secrets — Ocr:TesseractPath и Ocr:TessdataPath (подробно — TEAM_GUIDE.md).
public class OcrOptions
{
    // Путь к tesseract(.exe); "tesseract" — искать в PATH (так в Docker)
    public string TesseractPath { get; set; } = "tesseract";

    // Папка с *.traineddata; пусто — папка по умолчанию самого Tesseract
    public string TessdataPath { get; set; } = string.Empty;

    // Языки первого прохода: английский и турецкий. Русский здесь не нужен — на латинских бланках он подменяет буквы
    // кириллическими двойниками: «Name» → «Мате», «pg» → «Р9», «PAC001» → «РАСОО1»
    public string Languages { get; set; } = "eng+tur";

    // Второй проход, если первый прочитан неуверенно (средняя уверенность ниже FallbackBelowConfidence) —
    // так распознаётся русский бланк. Из двух результатов берётся более уверенный. Пусто — без второго прохода
    public string FallbackLanguages { get; set; } = "eng+rus+tur";
    public int FallbackBelowConfidence { get; set; } = 75;

    // Режим разбиения страницы Tesseract: 6 — «один блок текста», лучше всего сохраняет строки таблицы анализов
    public int PageSegmentationMode { get; set; } = 6;

    // Сколько ждать одну страницу; дольше — значит, что-то не так с сервером
    public int TimeoutSeconds { get; set; } = 60;

    // Сколько процессов Tesseract одновременно (каждый — сотни МБ памяти и ядро процессора)
    public int MaxConcurrency { get; set; } = 2;

    // Сколько страниц скана распознавать (одна страница — несколько секунд)
    public int MaxPages { get; set; } = 5;

    // Средняя уверенность Tesseract по словам (0–100), ниже которой фото считается нечитаемым → 422
    public int MinConfidence { get; set; } = 55;

    // Слово с уверенностью ниже этой помечается «[?]», а показатель с ним получает низкую confidence в ответе.
    // На тестовом фото верные слова — 88–97, ошибочные («7.1» → «7а») — 43–45
    public int UncertainWordConfidence { get; set; } = 70;
}
