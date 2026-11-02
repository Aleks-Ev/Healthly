using System.Diagnostics;
using HealthlyApi.Models;
using HealthlyApi.Services;
using HealthlyApi.Services.Ai;
using HealthlyApi.Services.Ai.Ocr;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SkiaSharp;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace HealthlyApi.Tests;

// OCR настоящим Tesseract: фото бланка и PDF-скан рисуются в памяти, затем распознаются.
// Если Tesseract не установлен, тесты пропускаются (см. TesseractFactAttribute) — остальные тесты от него не зависят.
public class OcrTests
{
    private static readonly string[] LabReport =
    [
        "CITY LABORATORY - BIOCHEMISTRY",
        "Patient: Elif Kaya",
        "Glucose 5.4 mmol/L 3.9-5.6",
        "Hemoglobin 135 g/L 120-160",
    ];

    [TesseractFact]
    public async Task Recognizes_a_photo_of_a_lab_report()
    {
        var text = await ExtractAsync(TestImages.Png(LabReport, fontSize: 28), "image/png");

        Assert.Contains("Glucose", text);
        Assert.Contains("5.4", text);
        Assert.Contains("Hemoglobin", text);
    }

    [TesseractFact]
    public async Task Recognizes_a_scanned_pdf_without_a_text_layer()
    {
        // PDF, в котором только картинка — как после сканера
        var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(595, 842);
        var png = TestImages.Png(LabReport, fontSize: 28);
        using var bitmap = SKBitmap.Decode(png);
        var height = 595.0 * bitmap.Height / bitmap.Width; // без искажения пропорций
        page.AddPng(png, new PdfRectangle(0, 842 - height, 595, 842));

        var text = await ExtractAsync(builder.Build(), "application/pdf");

        Assert.Contains("Glucose", text);
        Assert.Contains("5.4", text);
    }

    [TesseractFact]
    public async Task Recognized_text_is_anonymized()
    {
        var text = await ExtractAsync(TestImages.Png(LabReport, fontSize: 28), "image/png");

        var anonymized = PiiAnonymizer.Anonymize(text, ["Elif Kaya"]);

        Assert.DoesNotContain("Kaya", anonymized);
        Assert.Contains("Glucose", anonymized);
    }

    [TesseractFact]
    public async Task Russian_report_is_read_by_the_second_pass_and_latin_one_has_no_cyrillic()
    {
        if (!OcrTestOptions.HasLanguages("rus", "tur"))
        {
            return; // нет языковых файлов rus/tur — проверять нечего (см. TEAM_GUIDE.md)
        }
        var options = OcrTestOptions.WithLanguages("eng+tur", "eng+rus+tur");

        // Первый проход eng+tur неуверенный на кириллице → второй с русским
        var russian = await ExtractAsync(TestImages.Png(["Общий анализ крови", "Гемоглобин 135 г/л 120-160", "Лейкоциты 6.1 10^9/л 4.0-9.0"]), "image/png", options);
        Assert.Contains("Гемоглобин", russian);
        Assert.Contains("135", russian);

        // Латинский бланк — один проход без русского: никаких «Мате» вместо «Name»
        var latin = await ExtractAsync(TestImages.Png(["Name: Anna Berg", "MCH 28 27-31 pg", "Patient ID PAC001"]), "image/png", options);
        Assert.DoesNotMatch(@"\p{IsCyrillic}", latin);
    }

    private static async Task<string> ExtractAsync(byte[] file, string contentType, IOptions<OcrOptions>? options = null)
    {
        options ??= OcrTestOptions.Tesseract;
        var engine = new TesseractCliOcrEngine(options, NullLogger<TesseractCliOcrEngine>.Instance);
        var extractor = new DocumentTextExtractor(engine, options, NullLogger<DocumentTextExtractor>.Instance);
        await using var content = new MemoryStream(file);
        var input = new AiDocumentInput(1, "report", contentType, content, new AiPatientContext("Elif Kaya", "Female", 33), Array.Empty<ReferenceMedicalValue>());
        return (await extractor.ExtractTextAsync(input, CancellationToken.None)).Text;
    }
}

public static class OcrTestOptions
{
    public static readonly IOptions<OcrOptions> Default = Options.Create(new OcrOptions());

    // Где искать Tesseract: переменные HEALTHLY_TESSERACT_PATH / HEALTHLY_TESSDATA_PATH,
    // иначе стандартная установка на Windows (+ языки в %LOCALAPPDATA%\tessdata, см. TEAM_GUIDE.md), иначе PATH
    public static readonly IOptions<OcrOptions> Tesseract = Options.Create(new OcrOptions
    {
        TesseractPath = Environment.GetEnvironmentVariable("HEALTHLY_TESSERACT_PATH")
            ?? (File.Exists(@"C:\Program Files\Tesseract-OCR\tesseract.exe") ? @"C:\Program Files\Tesseract-OCR\tesseract.exe" : "tesseract"),
        TessdataPath = Environment.GetEnvironmentVariable("HEALTHLY_TESSDATA_PATH")
            ?? (Directory.Exists(LocalTessdata) ? LocalTessdata : string.Empty),
        Languages = "eng",
    });

    public static IOptions<OcrOptions> WithLanguages(string languages, string fallbackLanguages) => Options.Create(new OcrOptions
    {
        TesseractPath = Tesseract.Value.TesseractPath,
        TessdataPath = Tesseract.Value.TessdataPath,
        Languages = languages,
        FallbackLanguages = fallbackLanguages,
    });

    public static bool HasLanguages(params string[] languages)
    {
        var arguments = string.IsNullOrEmpty(Tesseract.Value.TessdataPath)
            ? "--list-langs"
            : $"--tessdata-dir \"{Tesseract.Value.TessdataPath}\" --list-langs";
        using var process = Process.Start(new ProcessStartInfo(Tesseract.Value.TesseractPath, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
        var output = process!.StandardOutput.ReadToEnd();
        process.WaitForExit();
        var installed = output.Split('\n', StringSplitOptions.TrimEntries);
        return languages.All(installed.Contains);
    }

    private static string LocalTessdata => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "tessdata");

    public static bool IsTesseractAvailable()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo(Tesseract.Value.TesseractPath, "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            return process != null && process.WaitForExit(10_000) && process.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

public sealed class TesseractFactAttribute : FactAttribute
{
    private static readonly bool Available = OcrTestOptions.IsTesseractAvailable();

    public TesseractFactAttribute()
    {
        if (!Available)
        {
            Skip = "Tesseract is not installed (see TEAM_GUIDE.md)";
        }
    }
}

public static class TestImages
{
    // Белая «страница» с чёрными строками текста — имитация фото бланка
    public static byte[] Png(IReadOnlyList<string> lines, float fontSize = 28)
    {
        const int width = 1200;
        var lineHeight = (int)(fontSize * 1.8f);
        using var surface = SKSurface.Create(new SKImageInfo(width, lineHeight * (lines.Count + 2)));
        surface.Canvas.Clear(SKColors.White);
        using var font = new SKFont(SKTypeface.Default, fontSize);
        using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
        for (var i = 0; i < lines.Count; i++)
        {
            surface.Canvas.DrawText(lines[i], 60, lineHeight * (i + 1.5f), SKTextAlign.Left, font, paint);
        }
        using var image = surface.Snapshot();
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }
}
