namespace HealthlyApi.Models;

// Сообщение ИИ-чата «Healthly AI Assistant». Хранится история пользователя (своя у каждого),
// в модель уходят только последние сообщения — и только после обезличивания (HealthChatPrompt).
// Удалили пользователя — удалилась и его переписка (ON DELETE CASCADE).
public class ChatMessage
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public string Role { get; set; } = ChatRoles.User; // ChatRoles: user / assistant
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class ChatRoles
{
    public const string User = "user";
    public const string Assistant = "assistant";
}
