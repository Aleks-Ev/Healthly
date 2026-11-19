using HealthlyApi.Data;
using HealthlyApi.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthlyApi.Services.Ai.Chat;

// ИИ-чат «Healthly AI Assistant»: история, отправка сообщения, очистка.
// Отправка:
//   1. из базы — профиль пациента, его подтверждённые показатели (TestResults, новые первыми), каталог лабораторий и врачей;
//   2. HealthChatContextBuilder оставляет только разрешённые поля (пол, возраст, рост, вес; без ФИО, e-mail, телефона,
//      даты рождения, города и Healthly ID);
//   3. HealthChatPrompt строит системный промпт и обезличивает последние реплики (окно HistoryWindow);
//   4. IChatCompletionClient: Gemini → Groq (FallbackChatClient) или заглушка MockChatClient без ключей;
//   5. сообщение пользователя и ответ сохраняются вместе — только если ответ получен (при 503 история не меняется).
// В базе хранится исходный текст пользователя (это его собственная переписка); обезличенная версия — только для модели.
public class HealthChatService
{
    public const int MaxMessageLength = 2000;
    public const int HistoryPageSize = 100;

    private readonly AppDbContext _context;
    private readonly IChatCompletionClient _client;

    public HealthChatService(AppDbContext context, IChatCompletionClient client)
    {
        _context = context;
        _client = client;
    }

    public Task<List<ChatMessage>> GetHistoryAsync(int userId, CancellationToken cancellationToken) =>
        _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Take(HistoryPageSize)
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

    public async Task<ChatExchange> SendAsync(int userId, string content, CancellationToken cancellationToken)
    {
        var user = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == userId, cancellationToken);

        var results = await _context.TestResults
            .AsNoTracking()
            .Where(r => r.MedicalDocument!.UserId == userId)
            .OrderByDescending(r => r.MedicalDocument!.UploadDate).ThenByDescending(r => r.Id)
            .Take(200)
            .ToListAsync(cancellationToken);
        var laboratories = await _context.Laboratories.AsNoTracking().ToListAsync(cancellationToken);
        var doctors = await _context.Users.AsNoTracking().Where(u => u.Role == Roles.Doctor).ToListAsync(cancellationToken);

        var history = await _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt).ThenByDescending(m => m.Id)
            .Take(HealthChatPrompt.HistoryWindow)
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .Select(m => new ChatTurn(m.Role, m.Content))
            .ToListAsync(cancellationToken);

        var chatContext = HealthChatContextBuilder.Build(user, results, laboratories, doctors);
        var identity = new HealthChatPrompt.PatientIdentity(user.FullName, user.Email, user.PatientCode);
        var request = HealthChatPrompt.Build(chatContext, history, content, identity);

        var completion = await _client.CompleteAsync(request, cancellationToken);

        var now = DateTime.UtcNow;
        var userMessage = new ChatMessage { UserId = userId, Role = ChatRoles.User, Content = content, CreatedAt = now };
        var assistantMessage = new ChatMessage
        {
            UserId = userId,
            Role = ChatRoles.Assistant,
            Content = completion.Content.Length > 8000 ? completion.Content[..8000] : completion.Content,
            // На миллисекунду позже — порядок сообщений стабилен даже при одинаковом времени
            CreatedAt = now.AddMilliseconds(1),
        };
        _context.ChatMessages.AddRange(userMessage, assistantMessage);
        await _context.SaveChangesAsync(cancellationToken);

        return new ChatExchange(userMessage, assistantMessage, completion.Model, completion.IsMock);
    }

    public Task<int> ClearAsync(int userId, CancellationToken cancellationToken) =>
        _context.ChatMessages.Where(m => m.UserId == userId).ExecuteDeleteAsync(cancellationToken);
}

public record ChatExchange(ChatMessage UserMessage, ChatMessage AssistantMessage, string Model, bool IsMock);
