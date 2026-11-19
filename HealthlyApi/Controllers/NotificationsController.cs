using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HealthlyApi.Data;
using HealthlyApi.Dtos;
using HealthlyApi.Services;

namespace HealthlyApi.Controllers;

// Уведомления текущего пользователя (колокольчик на сайте). Создаются в Services/Care/Notifier.
[ApiController]
[Route("api/notifications")]
[Tags("Notifications")]
[Authorize]
[Produces("application/json")]
public class NotificationsController : ControllerBase
{
    private const int PageSize = 50;
    private readonly AppDbContext _context;

    public NotificationsController(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>My notifications</summary>
    /// <remarks>The latest 50, newest first. <c>?unreadOnly=true</c> — only unread ones.</remarks>
    /// <response code="200">The notifications.</response>
    [HttpGet]
    [ProducesResponseType<List<NotificationResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<NotificationResponse>>> GetNotifications([FromQuery] bool unreadOnly, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        return await _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId && (!unreadOnly || n.ReadAt == null))
            .OrderByDescending(n => n.CreatedAt)
            .Take(PageSize)
            .Select(n => new NotificationResponse(n.Id, n.Type, n.ActorName, n.Title, n.Body, n.Link, n.CareConnectionId, n.CreatedAt, n.ReadAt))
            .ToListAsync(cancellationToken);
    }

    /// <summary>Number of unread notifications</summary>
    /// <remarks>Cheap enough to poll (the website checks it every minute).</remarks>
    /// <response code="200">The count.</response>
    [HttpGet("unread-count")]
    [ProducesResponseType<UnreadCountResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        return new UnreadCountResponse(await _context.Notifications.CountAsync(n => n.UserId == userId && n.ReadAt == null, cancellationToken));
    }

    /// <summary>Mark a notification as read</summary>
    /// <response code="204">Marked (or it was already read).</response>
    /// <response code="404">No such notification of the current user.</response>
    [HttpPost("{id:int}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var exists = await _context.Notifications.AnyAsync(n => n.Id == id && n.UserId == userId, cancellationToken);
        if (!exists)
        {
            return Problem(detail: "Notification not found", statusCode: StatusCodes.Status404NotFound);
        }

        await _context.Notifications
            .Where(n => n.Id == id && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow), cancellationToken);
        return NoContent();
    }

    /// <summary>Mark all my notifications as read</summary>
    /// <response code="204">Done.</response>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        await _context.Notifications
            .Where(n => n.UserId == userId && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, DateTime.UtcNow), cancellationToken);
        return NoContent();
    }
}
