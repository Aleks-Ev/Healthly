namespace HealthlyApi.Services.Ai;

// Сбой конкретного провайдера: сеть, таймаут, 429 (кончились лимиты Free Tier), 5xx, ответ не в формате JSON.
// FallbackAiDocumentProcessor ловит именно его и переключается на следующего провайдера.
public class AiProviderException : Exception
{
    public AiProviderException(string message, Exception? innerException = null) : base(message, innerException)
    {
    }
}

// Ошибка, которую нужно показать клиенту с конкретным кодом (ExceptionHandlingMiddleware → ProblemDetails):
//   422 — в документе нет текста (фото/скан без OCR), другой провайдер тут не поможет;
//   503 — все провайдеры недоступны.
public class AiAnalysisException : Exception
{
    public int StatusCode { get; }

    public AiAnalysisException(int statusCode, string message, Exception? innerException = null) : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
