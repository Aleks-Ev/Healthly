using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;

namespace HealthlyApi.Services.Ai.Ocr;

// OCR через программу Tesseract (командная строка), без нативных обёрток — одинаково работает на Windows и в Docker.
//   tesseract stdin stdout -l eng+tur --psm 6 -c tessedit_create_tsv=1 -c tessedit_create_txt=0
// Если первый проход неуверенный — второй с русским (Ocr:FallbackLanguages), берётся более уверенный результат.
// Картинка подаётся через stdin, результат читается из stdout — временные файлы с данными пациента на диск не пишутся.
// Формат tsv даёт по каждому слову номер строки и уверенность: из него собираем текст по строкам и среднюю уверенность.
// Аргументы передаются через ArgumentList (каждый отдельно), поэтому подстановка команд в них невозможна.
public class TesseractCliOcrEngine : IOcrEngine
{
    private readonly OcrOptions _options;
    private readonly ILogger<TesseractCliOcrEngine> _logger;
    private readonly SemaphoreSlim _concurrency;

    public TesseractCliOcrEngine(IOptions<OcrOptions> options, ILogger<TesseractCliOcrEngine> logger)
    {
        _options = options.Value;
        _logger = logger;
        _concurrency = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrency));
    }

    public async Task<OcrResult> RecognizeAsync(byte[] png, CancellationToken cancellationToken)
    {
        await _concurrency.WaitAsync(cancellationToken);
        try
        {
            var result = ParseTsv(await RunAsync(png, _options.Languages, cancellationToken), _options.UncertainWordConfidence);
            if (!string.IsNullOrWhiteSpace(_options.FallbackLanguages) && result.Confidence < _options.FallbackBelowConfidence)
            {
                var second = ParseTsv(await RunAsync(png, _options.FallbackLanguages, cancellationToken), _options.UncertainWordConfidence);
                _logger.LogInformation("OCR second pass ({Languages}): confidence {First} → {Second}",
                    _options.FallbackLanguages, result.Confidence, second.Confidence);
                if (second.Confidence > result.Confidence)
                {
                    result = second;
                }
            }
            return result;
        }
        finally
        {
            _concurrency.Release();
        }
    }

    private async Task<string> RunAsync(byte[] png, string languages, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(_options.TesseractPath)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in new[] { "stdin", "stdout", "-l", languages, "--psm", _options.PageSegmentationMode.ToString(CultureInfo.InvariantCulture) })
        {
            startInfo.ArgumentList.Add(argument);
        }
        if (!string.IsNullOrWhiteSpace(_options.TessdataPath))
        {
            startInfo.ArgumentList.Add("--tessdata-dir");
            startInfo.ArgumentList.Add(_options.TessdataPath);
        }
        // Формат tsv включаем параметрами, а не конфигом "tsv": конфиг лежит в tessdata/configs и не найдётся,
        // если языки взяты из своей папки (Ocr:TessdataPath) — тогда Tesseract молча выдаёт обычный текст
        foreach (var argument in new[] { "-c", "tessedit_create_tsv=1", "-c", "tessedit_create_txt=0" })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            _logger.LogError(exception, "Tesseract was not found at '{Path}'. Install it or set Ocr:TesseractPath", _options.TesseractPath);
            throw Unavailable(exception);
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));
        try
        {
            // stdout/stderr читаем параллельно с записью картинки, иначе на большом выводе процесс и мы ждём друг друга
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errors = process.StandardError.ReadToEndAsync(timeout.Token);
            await process.StandardInput.BaseStream.WriteAsync(png, timeout.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(timeout.Token);

            var tsv = await output;
            // Ошибки настройки (нет языка, неверный параметр) Tesseract иногда пишет в stderr, но завершается с кодом 0
            if (process.ExitCode != 0 || !tsv.StartsWith("level", StringComparison.Ordinal))
            {
                _logger.LogError("Tesseract failed (exit code {ExitCode}): {Errors}", process.ExitCode, await errors);
                throw Unavailable();
            }
            return tsv;
        }
        catch (OperationCanceledException exception)
        {
            StopProcess(process);
            if (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            _logger.LogError("Tesseract did not finish within {Seconds} s", _options.TimeoutSeconds);
            throw Unavailable(exception);
        }
    }

    // Колонки tsv: level page_num block_num par_num line_num word_num left top width height conf text; level 5 — слово.
    // Слова с уверенностью ниже uncertainBelow попадают в OcrResult.UncertainWords
    public static OcrResult ParseTsv(string tsv, double uncertainBelow)
    {
        var lines = new List<List<string>>();
        var confidences = new List<double>();
        var uncertain = new List<string>();
        string? currentLine = null;

        foreach (var row in tsv.Split('\n').Skip(1))
        {
            var columns = row.TrimEnd('\r').Split('\t');
            if (columns.Length < 12 || columns[0] != "5")
            {
                continue;
            }
            var word = columns[11].Trim();
            if (word.Length == 0)
            {
                continue;
            }

            var lineKey = $"{columns[1]}.{columns[2]}.{columns[3]}.{columns[4]}";
            if (lineKey != currentLine)
            {
                lines.Add([]);
                currentLine = lineKey;
            }
            if (double.TryParse(columns[10], NumberStyles.Float, CultureInfo.InvariantCulture, out var confidence) && confidence >= 0)
            {
                confidences.Add(confidence);
                if (confidence < uncertainBelow)
                {
                    uncertain.Add(word);
                }
            }
            lines[^1].Add(word);
        }

        var text = string.Join('\n', lines.Select(words => string.Join(' ', words)));
        return new OcrResult(text, confidences.Count == 0 ? 0 : Math.Round(confidences.Average(), 1), uncertain);
    }

    private static void StopProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // Процесс успел завершиться сам
        }
    }

    private static AiAnalysisException Unavailable(Exception? innerException = null) => new(
        StatusCodes.Status503ServiceUnavailable,
        "Text recognition for photos and scans is temporarily unavailable. Please try again later or upload a PDF.",
        innerException);
}
