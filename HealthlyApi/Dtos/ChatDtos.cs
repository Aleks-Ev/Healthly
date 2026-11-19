using System.ComponentModel.DataAnnotations;
using HealthlyApi.Models;
using HealthlyApi.Services.Ai.Chat;

namespace HealthlyApi.Dtos;

// Сообщение ИИ-чата. Role: "user" или "assistant"
public record ChatMessageResponse(int Id, string Role, string Content, DateTime CreatedAt)
{
    public static ChatMessageResponse From(ChatMessage message) => new(message.Id, message.Role, message.Content, message.CreatedAt);
}

// GET /api/chat/messages
public record ChatHistoryResponse(List<ChatMessageResponse> Messages, string Disclaimer);

// POST /api/chat/messages — оба сохранённых сообщения + дисклеймер для показа под ответом
public record ChatReplyResponse(
    ChatMessageResponse UserMessage,
    ChatMessageResponse AssistantMessage,
    string Model,     // "gemini:…", "groq:…" или "mock-chat-v1"
    bool IsMock,      // true — внешнего ИИ нет (нет ключей), ответ от заглушки по правилам
    string Disclaimer);

public class SendChatMessageRequest
{
    [Required(ErrorMessage = "Write a message")]
    [MaxLength(HealthChatService.MaxMessageLength, ErrorMessage = "The message is too long (2,000 characters at most)")]
    public string Content { get; set; } = string.Empty;
}
