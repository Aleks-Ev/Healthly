using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace HealthlyApi.Services.Ai;

// Резервный провайдер: Groq API (Free Tier, ключ — https://console.groq.com/keys).
// API совместим с OpenAI: POST https://api.groq.com/openai/v1/chat/completions, ключ — Authorization: Bearer.
// response_format = json_object заставляет модель вернуть валидный JSON (слово "JSON" обязано быть в промпте — оно там есть).
public class GroqAiDocumentProcessor : LlmDocumentProcessor
{
    private const string Endpoint = "https://api.groq.com/openai/v1/chat/completions";

    private readonly HttpClient _http;
    private readonly AiProviderOptions _provider;

    public GroqAiDocumentProcessor(HttpClient http, IOptions<AiOptions> options, DocumentTextExtractor extractor)
        : base(options.Value, extractor)
    {
        _http = http;
        _provider = options.Value.Groq;
    }

    protected override string ProviderName => "groq";
    protected override string ModelName => _provider.Model;

    protected override async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        var body = new
        {
            model = _provider.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
            temperature = 0.1,
            response_format = new { type = "json_object" },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = JsonContent.Create(body) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _provider.ApiKey);

        using var response = await _http.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new AiProviderException($"groq: HTTP {(int)response.StatusCode} — {Truncate(json)}");
        }

        // { "choices": [ { "message": { "content": "{...}" }, "finish_reason": "stop" } ] }
        try
        {
            using var document = JsonDocument.Parse(json);
            var choice = document.RootElement.GetProperty("choices")[0];
            var finishReason = choice.TryGetProperty("finish_reason", out var reason) ? reason.GetString() : null;
            if (finishReason is not null and not "stop")
            {
                // "length" — JSON обрезан по лимиту токенов
                throw new AiProviderException($"groq: generation stopped with finish_reason {finishReason}");
            }
            return choice.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new AiProviderException("groq: unexpected response format", exception);
        }
    }

    // Тело ошибки — в одну строку и коротко, чтобы лог оставался читаемым
    private static string Truncate(string text)
    {
        var line = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return line.Length <= 300 ? line : line[..300] + "…";
    }
}
