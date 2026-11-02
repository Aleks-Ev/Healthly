namespace HealthlyApi.Services.Ai;

// Настройки внешних ИИ-провайдеров (секция "Ai" в appsettings.json).
// Ключи API — только в user-secrets / переменных окружения, не в appsettings.json:
//   dotnet user-secrets set "Ai:Gemini:ApiKey" "<ключ из Google AI Studio>"
//   dotnet user-secrets set "Ai:Groq:ApiKey" "<ключ из console.groq.com>"
// Провайдер без ключа не используется; если ключей нет совсем — работает MockAiDocumentProcessor (см. Program.cs).
public class AiOptions
{
    // Сколько ждать ответа одного провайдера; по истечении — переход к следующему (FallbackAiDocumentProcessor)
    public int TimeoutSeconds { get; set; } = 30;

    // Ограничение длины текста документа, уходящего в модель (бесплатные лимиты считают токены)
    public int MaxInputChars { get; set; } = 20_000;

    public AiProviderOptions Gemini { get; set; } = new() { Model = "gemini-3.5-flash" };
    public AiProviderOptions Groq { get; set; } = new() { Model = "openai/gpt-oss-120b" };
}

public class AiProviderOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey) && !string.IsNullOrWhiteSpace(Model);
}
