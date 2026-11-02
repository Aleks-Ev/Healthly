namespace HealthlyApi.Services.Ai;

// Декоратор с отказоустойчивостью: пробует провайдеров по порядку (Gemini → Groq) и возвращает первый успешный ответ.
// На следующего переключается сразу, без повторов и пауз, при AiProviderException: сеть, таймаут,
// 429 (кончились лимиты Free Tier), 5xx, ответ не в нужном формате.
// Не переключается, если ошибка не зависит от провайдера: AiAnalysisException (в документе нет текста)
// и отмена запроса клиентом — они сразу уходят вызывающему коду.
// Если не ответил никто — AiAnalysisException 503.
public class FallbackAiDocumentProcessor : IAiDocumentProcessor
{
    private readonly IReadOnlyList<IAiDocumentProcessor> _processors;
    private readonly ILogger<FallbackAiDocumentProcessor> _logger;

    public FallbackAiDocumentProcessor(IReadOnlyList<IAiDocumentProcessor> processors, ILogger<FallbackAiDocumentProcessor> logger)
    {
        if (processors.Count == 0)
        {
            throw new ArgumentException("At least one AI document processor is required.", nameof(processors));
        }
        _processors = processors;
        _logger = logger;
    }

    public async Task<AiDocumentAnalysis> AnalyzeAsync(AiDocumentInput input, CancellationToken cancellationToken)
    {
        // Каждый провайдер читает файл с начала, поэтому поток должен уметь перематываться
        // (FileStream из FileStorageService умеет; иначе копируем в память)
        await using var buffered = input.Content.CanSeek ? null : new MemoryStream();
        if (buffered != null)
        {
            await input.Content.CopyToAsync(buffered, cancellationToken);
        }
        var content = buffered ?? input.Content;
        var start = content.Position;

        var failures = new List<AiProviderException>();
        foreach (var processor in _processors)
        {
            content.Position = start;
            try
            {
                return await processor.AnalyzeAsync(input with { Content = content }, cancellationToken);
            }
            catch (AiProviderException exception)
            {
                failures.Add(exception);
                _logger.LogWarning("AI provider {Provider} failed for document {DocumentId}, trying the next one: {Reason}",
                    processor.GetType().Name, input.DocumentId, exception.Message);
            }
        }

        _logger.LogError("All AI providers failed for document {DocumentId}: {Reasons}",
            input.DocumentId, string.Join(" | ", failures.Select(f => f.Message)));
        throw new AiAnalysisException(StatusCodes.Status503ServiceUnavailable,
            "The AI analysis service is temporarily unavailable. Please try again in a few minutes.",
            new AggregateException(failures));
    }
}
