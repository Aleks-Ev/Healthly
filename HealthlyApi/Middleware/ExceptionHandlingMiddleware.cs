using Microsoft.AspNetCore.Mvc;

namespace HealthlyApi.Middleware;

// Глобальная обработка исключений: любое необработанное исключение превращается в JSON ProblemDetails
// (application/problem+json) вместо HTML-страницы или обрыва соединения.
//
//   BadHttpRequestException (битый запрос, слишком большое тело) → его код (400, 413…)
//   любое другое исключение → 500; текст исключения виден только в Development, в Production — общая фраза
//
// Ошибки без исключений (401 от JWT, 403, 404 неизвестного адреса) дополняются телом ProblemDetails
// через UseStatusCodePages, а общие поля (traceId, instance, понятный detail) — в ProblemDetailsDefaults.
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetails)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Клиент сам закрыл соединение — отвечать некому, это не ошибка сервера
            _logger.LogDebug("Request aborted by the client: {Method} {Path}", context.Request.Method, context.Request.Path);
        }
        catch (Exception exception)
        {
            // Если ответ уже начал уходить клиенту, заголовки и код не поменять — пусть соединение оборвётся
            if (context.Response.HasStarted)
            {
                _logger.LogError(exception, "Unhandled exception after the response started: {Method} {Path}", context.Request.Method, context.Request.Path);
                throw;
            }

            var (status, detail) = Describe(exception);
            if (status >= StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(exception, "Unhandled exception: {Method} {Path}", context.Request.Method, context.Request.Path);
            }
            else
            {
                _logger.LogWarning("Bad request: {Message}", exception.Message);
            }

            // Clear() сбрасывает то, что успел записать обработчик; CORS-заголовки сохранятся (CorsMiddleware ставит их в OnStarting)
            context.Response.Clear();
            context.Response.StatusCode = status;

            await problemDetails.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                Exception = exception,
                ProblemDetails = new ProblemDetails { Status = status, Detail = detail },
            });
        }
    }

    private (int Status, string Detail) Describe(Exception exception) => exception switch
    {
        BadHttpRequestException badRequest => (badRequest.StatusCode, badRequest.Message),
        _ when _environment.IsDevelopment() => (StatusCodes.Status500InternalServerError, $"{exception.GetType().Name}: {exception.Message}"),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again later."),
    };
}

// Общий вид всех ProblemDetails в API — и от middleware, и от контроллеров (Problem(), ValidationProblem()), и от UseStatusCodePages
public static class ProblemDetailsDefaults
{
    public static void Customize(ProblemDetailsContext context)
    {
        var problem = context.ProblemDetails;
        var http = context.HttpContext;

        problem.Instance ??= $"{http.Request.Method} {http.Request.Path}";
        // traceId совпадает с логами сервера — по нему удобно искать ошибку
        problem.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? http.TraceIdentifier;

        // Понятное пояснение для ответов, у которых его нет (401/403 от авторизации, 404 неизвестного адреса)
        problem.Detail ??= problem.Status switch
        {
            StatusCodes.Status401Unauthorized => IsExpiredToken(http)
                ? "The access token has expired. Get a new one with POST /api/auth/refresh."
                : "The access token is missing or invalid. Log in to continue.",
            StatusCodes.Status403Forbidden => "You do not have permission to perform this action.",
            StatusCodes.Status404NotFound => "The requested resource was not found.",
            StatusCodes.Status405MethodNotAllowed => "This HTTP method is not supported for this endpoint.",
            _ => null,
        };
    }

    // JwtBearer пишет причину отказа в заголовок WWW-Authenticate: error_description="The token expired at ..."
    private static bool IsExpiredToken(HttpContext http) =>
        http.Response.Headers.WWWAuthenticate.ToString().Contains("expired", StringComparison.OrdinalIgnoreCase);
}
