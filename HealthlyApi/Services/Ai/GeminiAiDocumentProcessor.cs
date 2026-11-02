using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace HealthlyApi.Services.Ai;

// Основной провайдер: Google Gemini API (Free Tier, ключ из Google AI Studio — https://aistudio.google.com/apikey).
// POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent
// Ключ передаётся в заголовке x-goog-api-key, а не в адресе (?key=…), чтобы не попадать в логи HttpClient.
// В режиме responseMimeType = application/json модель возвращает чистый JSON без markdown.
public class GeminiAiDocumentProcessor : LlmDocumentProcessor
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";

    private readonly HttpClient _http;
    private readonly AiProviderOptions _provider;

    public GeminiAiDocumentProcessor(HttpClient http, IOptions<AiOptions> options, DocumentTextExtractor extractor)
        : base(options.Value, extractor)
    {
        _http = http;
        _provider = options.Value.Gemini;
    }

    protected override string ProviderName => "gemini";
    protected override string ModelName => _provider.Model;

    protected override async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = userPrompt } } } },
            generationConfig = new { responseMimeType = "application/json", temperature = 0.1 },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}{Uri.EscapeDataString(_provider.Model)}:generateContent")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("x-goog-api-key", _provider.ApiKey);

        using var response = await _http.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            // 429 — исчерпаны лимиты Free Tier (RESOURCE_EXHAUSTED), 5xx — сбой у Google: в обоих случаях — к резервному провайдеру
            throw new AiProviderException($"gemini: HTTP {(int)response.StatusCode} — {Truncate(json)}");
        }

        // { "candidates": [ { "content": { "parts": [ { "text": "{...}" } ] }, "finishReason": "STOP" } ] }
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                // Запрос заблокирован фильтрами безопасности: причина — в promptFeedback.blockReason
                throw new AiProviderException($"gemini: no candidates in the response — {Truncate(json)}");
            }

            var candidate = candidates[0];
            var finishReason = candidate.TryGetProperty("finishReason", out var reason) ? reason.GetString() : null;
            if (finishReason is not null and not "STOP")
            {
                // MAX_TOKENS — JSON обрезан, SAFETY/RECITATION — ответ заблокирован
                throw new AiProviderException($"gemini: generation stopped with finishReason {finishReason}");
            }

            var parts = candidate.GetProperty("content").GetProperty("parts");
            return string.Concat(parts.EnumerateArray()
                .Where(p => p.TryGetProperty("text", out _))
                .Select(p => p.GetProperty("text").GetString()));
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            throw new AiProviderException("gemini: unexpected response format", exception);
        }
    }

    // Тело ошибки — в одну строку и коротко, чтобы лог оставался читаемым
    private static string Truncate(string text)
    {
        var line = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return line.Length <= 300 ? line : line[..300] + "…";
    }
}
