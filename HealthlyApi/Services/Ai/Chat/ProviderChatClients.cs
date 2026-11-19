using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace HealthlyApi.Services.Ai.Chat;

// HTTP-клиенты чата к тем же провайдерам и с теми же ключами, что и ИИ-анализ документов (секция "Ai", user-secrets).
// В отличие от анализа здесь ответ — обычный текст (не JSON) и передаётся вся короткая история диалога.
// Любой сбой (сеть, таймаут, 429, 5xx, пустой или заблокированный ответ) → AiProviderException → FallbackChatClient
// переключается на следующего провайдера.

public abstract class ProviderChatClient : IChatCompletionClient
{
    // Лимит с запасом: у «думающих» моделей (Gemini 3.x, gpt-oss) рассуждения тратят тот же бюджет, что и ответ —
    // при 1200 токенах ответ обрывался на полуслове. Обрезанный ответ всё равно считается сбоем (см. ниже)
    protected const int MaxOutputTokens = 4096;
    protected const double Temperature = 0.3;

    private readonly int _timeoutSeconds;

    protected ProviderChatClient(int timeoutSeconds)
    {
        _timeoutSeconds = timeoutSeconds;
    }

    protected abstract string ProviderName { get; }
    protected abstract string ModelName { get; }

    protected abstract Task<string> SendAsync(ChatCompletionRequest request, CancellationToken cancellationToken);

    public async Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken)
    {
        string content;
        try
        {
            content = await SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new AiProviderException($"{ProviderName}: network error — {exception.Message}", exception);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiProviderException($"{ProviderName}: no response within {_timeoutSeconds} s", exception);
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new AiProviderException($"{ProviderName}: empty answer");
        }
        return new ChatCompletionResult(content.Trim(), $"{ProviderName}:{ModelName}", IsMock: false);
    }

    protected static string Truncate(string text)
    {
        var line = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return line.Length <= 300 ? line : line[..300] + "…";
    }
}

// Google Gemini: роли в истории — "user" и "model", системный промпт — systemInstruction
public class GeminiChatClient : ProviderChatClient
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models/";
    private readonly HttpClient _http;
    private readonly AiProviderOptions _provider;

    public GeminiChatClient(HttpClient http, IOptions<AiOptions> options) : base(options.Value.TimeoutSeconds)
    {
        _http = http;
        _provider = options.Value.Gemini;
    }

    protected override string ProviderName => "gemini";
    protected override string ModelName => _provider.Model;

    protected override async Task<string> SendAsync(ChatCompletionRequest request, CancellationToken cancellationToken)
    {
        var body = new
        {
            systemInstruction = new { parts = new[] { new { text = request.SystemPrompt } } },
            contents = request.Messages.Select(m => new
            {
                role = m.Role == Models.ChatRoles.Assistant ? "model" : "user",
                parts = new[] { new { text = m.Content } },
            }).ToArray(),
            generationConfig = new { temperature = Temperature, maxOutputTokens = MaxOutputTokens },
        };

        using var message = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}{Uri.EscapeDataString(_provider.Model)}:generateContent")
        {
            Content = JsonContent.Create(body),
        };
        message.Headers.Add("x-goog-api-key", _provider.ApiKey);

        using var response = await _http.SendAsync(message, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new AiProviderException($"gemini: HTTP {(int)response.StatusCode} — {Truncate(json)}");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                throw new AiProviderException($"gemini: no candidates in the response — {Truncate(json)}");
            }
            var candidate = candidates[0];
            var finishReason = candidate.TryGetProperty("finishReason", out var reason) ? reason.GetString() : null;
            // MAX_TOKENS — ответ оборван (пациент не должен получить полфразы), SAFETY/RECITATION — ответа нет:
            // в обоих случаях — к следующему провайдеру
            if (finishReason is not null and not "STOP")
            {
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
}

// Groq (совместим с OpenAI Chat Completions): system + user/assistant
public class GroqChatClient : ProviderChatClient
{
    private const string Endpoint = "https://api.groq.com/openai/v1/chat/completions";
    private readonly HttpClient _http;
    private readonly AiProviderOptions _provider;

    public GroqChatClient(HttpClient http, IOptions<AiOptions> options) : base(options.Value.TimeoutSeconds)
    {
        _http = http;
        _provider = options.Value.Groq;
    }

    protected override string ProviderName => "groq";
    protected override string ModelName => _provider.Model;

    protected override async Task<string> SendAsync(ChatCompletionRequest request, CancellationToken cancellationToken)
    {
        var messages = new List<object> { new { role = "system", content = request.SystemPrompt } };
        messages.AddRange(request.Messages.Select(m => (object)new
        {
            role = m.Role == Models.ChatRoles.Assistant ? "assistant" : "user",
            content = m.Content,
        }));

        var body = new { model = _provider.Model, messages, temperature = Temperature, max_tokens = MaxOutputTokens };

        using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint) { Content = JsonContent.Create(body) };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _provider.ApiKey);

        using var response = await _http.SendAsync(message, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new AiProviderException($"groq: HTTP {(int)response.StatusCode} — {Truncate(json)}");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var choice = document.RootElement.GetProperty("choices")[0];
            var finishReason = choice.TryGetProperty("finish_reason", out var reason) ? reason.GetString() : null;
            if (finishReason is not null and not "stop")
            {
                // "length" — ответ оборван по лимиту токенов
                throw new AiProviderException($"groq: generation stopped with finish_reason {finishReason}");
            }
            return choice.GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException)
        {
            throw new AiProviderException("groq: unexpected response format", exception);
        }
    }
}

// Цепочка провайдеров Gemini → Groq: первый успешный ответ. Переключение сразу, без повторов.
// Не ответил никто — AiAnalysisException 503 (ExceptionHandlingMiddleware → ProblemDetails)
public class FallbackChatClient : IChatCompletionClient
{
    private readonly IReadOnlyList<IChatCompletionClient> _clients;
    private readonly ILogger<FallbackChatClient> _logger;

    public FallbackChatClient(IReadOnlyList<IChatCompletionClient> clients, ILogger<FallbackChatClient> logger)
    {
        if (clients.Count == 0)
        {
            throw new ArgumentException("At least one chat client is required.", nameof(clients));
        }
        _clients = clients;
        _logger = logger;
    }

    public async Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken)
    {
        var failures = new List<AiProviderException>();
        foreach (var client in _clients)
        {
            try
            {
                return await client.CompleteAsync(request, cancellationToken);
            }
            catch (AiProviderException exception)
            {
                failures.Add(exception);
                _logger.LogWarning("Chat provider {Provider} failed, trying the next one: {Reason}", client.GetType().Name, exception.Message);
            }
        }

        _logger.LogError("All chat providers failed: {Reasons}", string.Join(" | ", failures.Select(f => f.Message)));
        throw new AiAnalysisException(StatusCodes.Status503ServiceUnavailable,
            "Healthly AI is temporarily unavailable. Please try again in a few minutes.",
            new AggregateException(failures));
    }
}
