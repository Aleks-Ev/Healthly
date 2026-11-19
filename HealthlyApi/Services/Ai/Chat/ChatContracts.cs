namespace HealthlyApi.Services.Ai.Chat;

// Контракты ИИ-чата. Всё, что здесь лежит, уже обезличено: HealthChatContextBuilder собирает контекст только из
// разрешённых полей, HealthChatPrompt прогоняет данные пациента и его сообщения через PiiAnonymizer.

// Реплика диалога для модели: Role — ChatRoles.User / ChatRoles.Assistant
public record ChatTurn(string Role, string Content);

// Обезличенный профиль: только пол, возраст (полных лет), рост, вес и группа крови.
// Имени, e-mail, телефона, даты рождения, города и Healthly ID здесь нет и быть не должно (тесты HealthChatPrivacyTests).
// BloodType пока всегда null: в модели User такого поля нет — оставлено, чтобы подключить без изменения промпта.
public record ChatPatientProfile(string? Sex, int? Age, double? HeightCm, double? WeightKg, string? BloodType);

// Последний подтверждённый результат по каждому показателю (из TestResults) — без дат и названий файлов
public record ChatLabResult(string TestName, string Value, string Unit, string ReferenceRange, bool IsAbnormal, bool IsCritical);

// Публичный каталог платформы (не данные пациента): лаборатории-партнёры и врачи, принимающие пациентов
public record ChatLaboratoryInfo(string Name, string City, string Address, string OpeningHours);
public record ChatDoctorInfo(string FullName, string Specialty, string Workplace, string City);

public record HealthChatContext(
    ChatPatientProfile Profile,
    IReadOnlyList<ChatLabResult> Results,
    IReadOnlyList<ChatLaboratoryInfo> Laboratories,
    IReadOnlyList<ChatDoctorInfo> Doctors);

// Ровно то, что уходит провайдеру: системный промпт + последние реплики (последняя — новое сообщение пользователя).
// Context — тот же контекст в структурированном виде, нужен только заглушке MockChatClient (во внешний ИИ он не уходит)
public record ChatCompletionRequest(string SystemPrompt, IReadOnlyList<ChatTurn> Messages, HealthChatContext Context);

public record ChatCompletionResult(string Content, string Model, bool IsMock);

// Один провайдер (Gemini, Groq) или их цепочка (FallbackChatClient). Сбой провайдера — AiProviderException
public interface IChatCompletionClient
{
    Task<ChatCompletionResult> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken);
}
