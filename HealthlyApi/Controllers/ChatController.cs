using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using HealthlyApi.Dtos;
using HealthlyApi.Services;
using HealthlyApi.Services.Ai.Chat;

namespace HealthlyApi.Controllers;

// ИИ-чат «Healthly AI Assistant» — у каждого пользователя своя история.
// Ассистент отвечает только о здоровье и о платформе Healthly; в модель уходит обезличенный контекст (HealthChatService).
[ApiController]
[Route("api/chat/messages")]
[Tags("AI chat")]
[Authorize]
[Produces("application/json")]
public class ChatController : ControllerBase
{
    private readonly HealthChatService _chat;

    public ChatController(HealthChatService chat)
    {
        _chat = chat;
    }

    /// <summary>My chat history</summary>
    /// <remarks>The latest 100 messages, oldest first. <c>disclaimer</c> is the text to show under every assistant message.</remarks>
    /// <response code="200">The history (empty for a new user).</response>
    [HttpGet]
    [ProducesResponseType<ChatHistoryResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatHistoryResponse>> GetMessages(CancellationToken cancellationToken)
    {
        var messages = await _chat.GetHistoryAsync(User.GetUserId()!.Value, cancellationToken);
        return new ChatHistoryResponse(messages.Select(ChatMessageResponse.From).ToList(), HealthChatPrompt.Disclaimer);
    }

    /// <summary>Send a message to Healthly AI</summary>
    /// <remarks>
    /// <c>{ "content": "…" }</c>, up to 2,000 characters. The assistant answers only about health and the Healthly platform,
    /// sees an anonymized profile (sex, age, height, weight) and the latest confirmed lab results — never the name, e-mail,
    /// phone, date of birth, city or Healthly ID. Both messages are saved only when an answer was received.
    /// Limits: 10 messages per minute and 30 per hour per user.
    /// </remarks>
    /// <response code="200">The saved user message and the assistant's answer.</response>
    /// <response code="400">Empty or too long message; see <c>errors</c>.</response>
    /// <response code="404">The account no longer exists.</response>
    /// <response code="429">Too many messages; wait for the time in <c>Retry-After</c> (seconds).</response>
    /// <response code="503">All AI providers are unavailable; nothing was saved, try again later.</response>
    [HttpPost]
    [EnableRateLimiting(RateLimiting.AiChat)]
    [ProducesResponseType<ChatReplyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ChatReplyResponse>> SendMessage(SendChatMessageRequest request, CancellationToken cancellationToken)
    {
        var content = request.Content.Trim();
        if (content.Length == 0)
        {
            ModelState.AddModelError(nameof(request.Content), "Write a message");
            return ValidationProblem(ModelState);
        }

        var exchange = await _chat.SendAsync(User.GetUserId()!.Value, content, cancellationToken);
        if (exchange is null)
        {
            return Problem(detail: "Your account was not found. Log in again.", statusCode: StatusCodes.Status404NotFound);
        }
        return new ChatReplyResponse(
            ChatMessageResponse.From(exchange.UserMessage),
            ChatMessageResponse.From(exchange.AssistantMessage),
            exchange.Model,
            exchange.IsMock,
            HealthChatPrompt.Disclaimer);
    }

    /// <summary>Clear my chat history</summary>
    /// <response code="204">History deleted.</response>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ClearMessages(CancellationToken cancellationToken)
    {
        await _chat.ClearAsync(User.GetUserId()!.Value, cancellationToken);
        return NoContent();
    }
}
