namespace HealthlyApi.Models;

// Уведомление на сайте/в приложении (колокольчик). Title и Body — готовый текст на английском (для мобильного приложения);
// сайт по Type и ActorName может показать перевод. Link — путь на сайте, куда ведёт уведомление ("/care").
public class Notification
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public string Type { get; set; } = string.Empty;   // NotificationTypes
    public string ActorName { get; set; } = string.Empty; // «кто» — врач, лаборатория или пациент
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;

    public int? CareConnectionId { get; set; }
    public CareConnection? CareConnection { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
}

public static class NotificationTypes
{
    public const string CareRequestReceived = "CareRequestReceived"; // врачу / сотрудникам лаборатории
    public const string CareRequestAccepted = "CareRequestAccepted"; // пациенту: «поделиться данными?»
    public const string CareRequestDeclined = "CareRequestDeclined"; // пациенту
    public const string CareSharingConfirmed = "CareSharingConfirmed"; // врачу / лаборатории
    public const string CareConnectionEnded = "CareConnectionEnded"; // второй стороне
}
