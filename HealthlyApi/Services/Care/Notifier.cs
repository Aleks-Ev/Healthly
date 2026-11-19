using HealthlyApi.Data;
using HealthlyApi.Models;
using Microsoft.EntityFrameworkCore;

namespace HealthlyApi.Services.Care;

// Уведомления (колокольчик на сайте, список в приложении).
// Как и AuditLog, только добавляет записи в контекст — они сохраняются тем же SaveChanges, что и само действие:
// не бывает «связь принята, а уведомление потерялось».
public class Notifier
{
    // Куда ведут уведомления о связях на сайте
    public const string CareLink = "/care";
    public const string LabLink = "/lab";

    private readonly AppDbContext _context;

    public Notifier(AppDbContext context)
    {
        _context = context;
    }

    public void ToUser(int userId, string type, string actorName, string title, string body, string link, CareConnection? connection = null)
    {
        _context.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = type,
            ActorName = Truncate(actorName, 200),
            Title = Truncate(title, 200),
            Body = Truncate(body, 500),
            Link = link,
            CareConnection = connection,
        });
    }

    // Всем сотрудникам лаборатории (запрос может принять любой из них)
    public async Task ToLaboratoryStaffAsync(int laboratoryId, string type, string actorName, string title, string body, CareConnection connection, CancellationToken cancellationToken)
    {
        var staffIds = await _context.Users
            .Where(u => u.Role == Roles.LabStaff && u.LaboratoryId == laboratoryId)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        foreach (var staffId in staffIds)
        {
            ToUser(staffId, type, actorName, title, body, LabLink, connection);
        }
    }

    private static string Truncate(string value, int max) => value.Length > max ? value[..max] : value;
}
