using HealthlyApi.Services.Ai.Ocr;
using Microsoft.Extensions.Options;
using PDFtoImage;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace HealthlyApi.Services.Ai;

// Извлекает текст документа локально, на нашем сервере — до обезличивания и отправки во внешний ИИ.
// Сам файл (PDF/фото) наружу не уходит никогда: в нём имя, телефон и т. д., которые PiiAnonymizer не может убрать из картинки.
//
//   PDF, страница с текстовым слоем  → PdfPig (быстро и точно)
//   PDF, страница-скан (нет текста)  → PDFtoImage рисует её в картинку 300 DPI → OCR
//   фото PNG/JPG                     → ImagePreprocessor (поворот, серый, масштаб) → OCR
// OCR-текст ниже порога уверенности (Ocr:MinConfidence) отбрасывается: лучше честный 422 «переснимите фото»,
// чем показатели, придуманные моделью по мусору.
//
// Scoped: результат запоминается на время запроса, поэтому при переключении Gemini → Groq OCR не запускается второй раз.
public class DocumentTextExtractor
{
    private const int MaxPdfPages = 20;
    private const int MinPageChars = 20; // меньше букв и цифр на странице PDF — считаем её сканом
    private const int OcrDpi = 300;

    private readonly IOcrEngine _ocr;
    private readonly OcrOptions _options;
    private readonly ILogger<DocumentTextExtractor> _logger;
    private readonly Dictionary<int, ExtractedText> _cache = [];

    public DocumentTextExtractor(IOcrEngine ocr, IOptions<OcrOptions> options, ILogger<DocumentTextExtractor> logger)
    {
        _ocr = ocr;
        _options = options.Value;
        _logger = logger;
    }

    // Всегда возвращает непустой текст; если прочитать нечего — AiAnalysisException 422 с подсказкой для пользователя
    public async Task<ExtractedText> ExtractTextAsync(AiDocumentInput input, CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(input.DocumentId, out var cached))
        {
            return cached;
        }

        using var buffer = new MemoryStream();
        await input.Content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();

        var uncertainWords = new List<string>();
        var text = input.ContentType.ToLowerInvariant() switch
        {
            "application/pdf" => await ExtractFromPdfAsync(input.DocumentId, bytes, uncertainWords, cancellationToken),
            "image/png" or "image/jpeg" => await ExtractFromImageAsync(input.DocumentId, bytes, uncertainWords, cancellationToken),
            _ => throw Unreadable("This file type cannot be analyzed. Upload a PDF, PNG or JPG lab report."),
        };

        var result = new ExtractedText(text, uncertainWords);
        _cache[input.DocumentId] = result;
        return result;
    }

    private async Task<string> ExtractFromPdfAsync(int documentId, byte[] pdf, List<string> uncertainWords, CancellationToken cancellationToken)
    {
        List<string?> pages; // null — у страницы нет текстового слоя, нужен OCR
        try
        {
            using var document = PdfDocument.Open(pdf);
            // ContentOrderTextExtractor сохраняет строки таблицы («Glucose 5.4 mmol/L 3.9-5.6»), page.Text склеил бы всё подряд
            pages = document.GetPages()
                .Take(MaxPdfPages)
                .Select(page => ContentOrderTextExtractor.GetText(page).Trim())
                .Select(text => CountChars(text) >= MinPageChars ? text : null)
                .ToList();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw Unreadable("The PDF file is damaged or password-protected.", exception);
        }

        var scannedPages = pages.Select((text, index) => (text, index)).Where(p => p.text == null).Select(p => p.index).ToList();
        foreach (var index in scannedPages.Take(_options.MaxPages))
        {
            // PDFium (внутри PDFtoImage) собран только под эти ОС; сервер у нас — Windows или Linux (Docker)
            if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS())
            {
                break;
            }
            using var bitmap = Conversion.ToImage(pdf, index, password: null, new RenderOptions(Dpi: OcrDpi, Grayscale: true));
            pages[index] = await RecognizeAsync(documentId, $"page {index + 1}", ImagePreprocessor.ToPng(bitmap), uncertainWords, cancellationToken);
        }

        var text = string.Join("\n\n", pages.Where(p => !string.IsNullOrEmpty(p)));
        if (text.Length == 0)
        {
            throw scannedPages.Count > 0 ? NotReadable() : Unreadable("No text was found in this PDF.");
        }
        return text;
    }

    private async Task<string> ExtractFromImageAsync(int documentId, byte[] image, List<string> uncertainWords, CancellationToken cancellationToken)
    {
        var png = ImagePreprocessor.PrepareForOcr(image) ?? throw Unreadable("The image file is damaged or too large.");
        return await RecognizeAsync(documentId, "image", png, uncertainWords, cancellationToken) ?? throw NotReadable();
    }

    // null — распознано слишком мало или неуверенно; неуверенные слова принятой страницы добавляются в uncertainWords
    private async Task<string?> RecognizeAsync(int documentId, string part, byte[] png, List<string> uncertainWords, CancellationToken cancellationToken)
    {
        var result = await _ocr.RecognizeAsync(png, cancellationToken);
        var chars = CountChars(result.Text);
        // В лог — только числа, не текст: в нём персональные данные
        _logger.LogInformation("OCR of document {DocumentId} ({Part}): {Chars} characters, confidence {Confidence}, uncertain words {Uncertain}",
            documentId, part, chars, result.Confidence, result.UncertainWords.Count);

        if (chars < MinPageChars || result.Confidence < _options.MinConfidence)
        {
            return null;
        }
        uncertainWords.AddRange(result.UncertainWords);
        return result.Text;
    }

    private static int CountChars(string text) => text.Count(char.IsLetterOrDigit);

    private static AiAnalysisException NotReadable() => Unreadable(
        "The text in this photo or scan could not be read reliably. Take the photo in good light, with the page flat and in focus, or upload a PDF.");

    private static AiAnalysisException Unreadable(string message, Exception? innerException = null) =>
        new(StatusCodes.Status422UnprocessableEntity, message, innerException);
}

// Текст документа и слова, которые OCR прочитал неуверенно (для PDF с текстовым слоем список пуст)
public record ExtractedText(string Text, IReadOnlyList<string> UncertainWords);
