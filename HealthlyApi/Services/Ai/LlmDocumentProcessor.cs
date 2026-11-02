using System.Globalization;
using System.Text;
using System.Text.Json;
using HealthlyApi.Models;

namespace HealthlyApi.Services.Ai;

// Общая часть реальных ИИ-процессоров (Gemini, Groq): одинаковый конвейер, разный только HTTP-запрос к модели.
//   1. текст документа извлекается локально (DocumentTextExtractor: текстовый слой PDF или OCR) — файл наружу не уходит;
//   2. текст обезличивается PiiAnonymizer.Anonymize (с ФИО пациента из базы) — ТОЛЬКО он отправляется в модель;
//   3. модель извлекает показатели и пишет резюме в JSON; «в норме / не в норме» она НЕ решает —
//      это делает DocumentAnalysisService по справочнику ReferenceMedicalValues;
//   4. ответ модели проверяется и приводится к AiDocumentAnalysis.
// Любой сбой провайдера (сеть, таймаут, 429, 5xx, битый JSON) → AiProviderException → FallbackAiDocumentProcessor
// переключается на следующего провайдера.
public abstract class LlmDocumentProcessor : IAiDocumentProcessor
{
    private const double DefaultConfidence = 0.5; // модель не указала уверенность
    private const double UncertainOcrConfidence = DocumentAnalysisService.UncertainConfidence; // значение прочитано OCR неуверенно
    private const int MaxRecommendations = 5;

    private readonly AiOptions _options;
    private readonly DocumentTextExtractor _extractor;

    protected LlmDocumentProcessor(AiOptions options, DocumentTextExtractor extractor)
    {
        _options = options;
        _extractor = extractor;
    }

    // Для логов и поля Model ответа: "gemini", "groq"
    protected abstract string ProviderName { get; }
    protected abstract string ModelName { get; }

    // Отправляет промпт модели и возвращает её текстовый ответ (ожидается JSON)
    protected abstract Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken);

    public async Task<AiDocumentAnalysis> AnalyzeAsync(AiDocumentInput input, CancellationToken cancellationToken)
    {
        // Нечего прочитать (битый файл, размытое фото) → 422 изнутри экстрактора; провайдера при этом не меняем
        var extracted = await _extractor.ExtractTextAsync(input, cancellationToken);

        // Главное правило безопасности: в модель уходит только обезличенный текст
        var anonymizedText = PiiAnonymizer.Anonymize(extracted.Text, [input.Patient.FullName]);
        if (anonymizedText.Length > _options.MaxInputChars)
        {
            anonymizedText = anonymizedText[.._options.MaxInputChars];
        }

        string response;
        try
        {
            response = await CompleteAsync(BuildSystemPrompt(input.ReferenceValues), anonymizedText, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new AiProviderException($"{ProviderName}: network error — {exception.Message}", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Отмена не по запросу клиента — значит, сработал HttpClient.Timeout
            throw new AiProviderException($"{ProviderName}: no response within {_options.TimeoutSeconds} s", exception);
        }

        var (indicators, summary, recommendations) = ParseResponse(response, ProviderName);
        indicators = MarkUncertainOcrValues(indicators, extracted.UncertainWords);

        return new AiDocumentAnalysis(
            indicators,
            summary,
            recommendations,
            anonymizedText,
            $"{ProviderName}:{ModelName}",
            IsMock: false);
    }

    // Нормы передаём только как список названий и единиц — чтобы модель называла показатели так же, как справочник.
    // Возраст, пол и имя пациента в модель не отправляются: для извлечения показателей они не нужны.
    private static string BuildSystemPrompt(IReadOnlyList<ReferenceMedicalValue> references)
    {
        var catalog = new StringBuilder();
        foreach (var reference in references.DistinctBy(r => r.MetricName, StringComparer.OrdinalIgnoreCase))
        {
            catalog.Append("- ").Append(reference.MetricName);
            if (!string.IsNullOrEmpty(reference.Unit))
            {
                catalog.Append(" — usually in ").Append(reference.Unit);
            }
            catalog.Append('\n');
        }

        return $$"""
            You extract laboratory test results from the text of a medical lab report.
            The text has already been anonymized: placeholders such as [REDACTED_NAME], [REDACTED_DATE], [REDACTED_PHONE] replace personal data. Never try to restore or guess them.

            Rules:
            1. Extract every measured indicator: its name, the value exactly as printed, the unit, and the reference range printed on the report (empty string if there is none).
               "referenceRange" is only the range itself, without units or words: "3.9-5.6", "<5.2", ">=60" (">OR= 60" means ">=60", "Up to 15" means "<=15").
               The text may come from OCR of a photo and contain recognition errors (for example "7а" or "1079/L"). Copy values and units exactly as they appear; never correct, convert or guess them, and give such values a low confidence.
            2. If an indicator is one of the catalog indicators below (also under another name, abbreviation or language, e.g. "WBC" or "Total Leucocyte Count" is Leukocytes), use the catalog name exactly as "testName", without a unit. Keep the unit printed on the report — never convert values.
            3. Do NOT decide or state whether any value is normal, abnormal, high or low, and do not diagnose. The application evaluates the values against its own reference database.
            4. "summary": 2-4 sentences in plain English for the patient: what kind of report this is and which indicators or body systems it covers.
            5. "recommendations": up to 3 short, general, non-diagnostic tips in English (for example, discussing the report with a doctor or tracking results over time).
            6. "confidence": a number from 0 to 1 — how sure you are that the name and value were read correctly from the text.
            7. If the text is not a lab report, return an empty "indicators" array and explain that in "summary".

            Catalog indicators:
            {{catalog}}
            Reply with a single JSON object only, no markdown, in exactly this format:
            {"indicators":[{"testName":"Glucose","value":"5.4","unit":"mmol/L","referenceRange":"3.9-5.6","confidence":0.95}],"summary":"...","recommendations":["..."]}
            """;
    }

    // Модель может ошибиться форматом — проверяем всё сами, а не доверяем ответу.
    // public — для юнит-тестов (HealthlyApi.Tests)
    public static (List<AiExtractedIndicator> Indicators, string Summary, List<string> Recommendations) ParseResponse(
        string response, string providerName)
    {
        try
        {
            using var json = JsonDocument.Parse(StripCodeFence(response));
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new AiProviderException($"{providerName}: the response is not a JSON object");
            }

            var indicators = new List<AiExtractedIndicator>();
            if (root.TryGetProperty("indicators", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray().Where(i => i.ValueKind == JsonValueKind.Object))
                {
                    var name = GetString(item, "testName");
                    var value = GetString(item, "value");
                    if (name.Length == 0 || value.Length == 0)
                    {
                        continue;
                    }
                    indicators.Add(new AiExtractedIndicator(
                        name,
                        value,
                        GetString(item, "unit"),
                        GetString(item, "referenceRange"),
                        GetConfidence(item)));
                }
            }

            var summary = GetString(root, "summary");
            var recommendations = root.TryGetProperty("recommendations", out var tips) && tips.ValueKind == JsonValueKind.Array
                ? tips.EnumerateArray()
                    .Where(t => t.ValueKind == JsonValueKind.String)
                    .Select(t => t.GetString()!.Trim())
                    .Where(t => t.Length > 0)
                    .Take(MaxRecommendations)
                    .ToList()
                : [];

            if (indicators.Count == 0 && summary.Length == 0)
            {
                throw new AiProviderException($"{providerName}: the response contains neither indicators nor a summary");
            }

            return (indicators, summary, recommendations);
        }
        catch (JsonException exception)
        {
            throw new AiProviderException($"{providerName}: the response is not valid JSON", exception);
        }
    }

    // Решение «доверять ли значению» — в коде, а не у модели: если OCR прочитал значение показателя неуверенно,
    // confidence снижается, даже если модель поставила высокую. Только значение, не единицу: одна неуверенно
    // прочитанная «/cumm» где-то на странице иначе снижала бы уверенность у всех показателей с этой единицей. Клиент по низкой confidence просит
    // пользователя сверить значение с бланком. Кириллические двойники латиницы («7а» и «7a») считаются одним словом.
    private static List<AiExtractedIndicator> MarkUncertainOcrValues(List<AiExtractedIndicator> indicators, IReadOnlyList<string> uncertainWords)
    {
        if (uncertainWords.Count == 0)
        {
            return indicators;
        }
        var uncertain = uncertainWords.Select(FoldForComparison).ToHashSet();
        return indicators
            .Select(indicator => indicator.Value
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Any(word => uncertain.Contains(FoldForComparison(word)))
                    ? indicator with { Confidence = Math.Min(indicator.Confidence, UncertainOcrConfidence) }
                    : indicator)
            .ToList();
    }

    private static string FoldForComparison(string word) => new(word
        .Trim('.', ',', ';', ':', '(', ')')
        .ToLowerInvariant()
        .Select(c => c switch
        {
            'а' => 'a', 'е' => 'e', 'о' => 'o', 'р' => 'p', 'с' => 'c', 'х' => 'x', 'у' => 'y', 'к' => 'k',
            var other => other,
        })
        .ToArray());

    // Некоторые модели оборачивают JSON в ```json … ``` несмотря на инструкцию
    private static string StripCodeFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }
        var start = trimmed.IndexOf('\n');
        var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return start >= 0 && end > start ? trimmed[(start + 1)..end].Trim() : trimmed;
    }

    // Значение может прийти и строкой ("5.4"), и числом (5.4) — в AiExtractedIndicator оно всегда строка
    private static string GetString(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return string.Empty;
        }
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString()!.Trim(),
            JsonValueKind.Number => value.GetRawText(),
            _ => string.Empty,
        };
    }

    private static double GetConfidence(JsonElement item)
    {
        if (!item.TryGetProperty("confidence", out var value))
        {
            return DefaultConfidence;
        }
        var confidence = value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String when double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => DefaultConfidence,
        };
        return Math.Round(Math.Clamp(confidence, 0, 1), 2);
    }
}
