using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace HealthlyApi.Services;

// Ограничение частоты запросов — когда сайт открыт в интернет (Cloudflare Tunnel, см. TEAM_GUIDE.md):
// - вход и регистрация — по IP: защита от подбора паролей и массовой регистрации;
// - ИИ-анализ — по пользователю: чужой человек не израсходует бесплатную квоту Gemini/Groq;
// - поиск пациента по Healthly ID — по сотруднику лаборатории: защита от перебора кодов.
// Лимиты с запасом: группа студентов из одной сети (один внешний IP) спокойно регистрируется одновременно.
// Превышение → 429 с телом ProblemDetails (UseStatusCodePages) и заголовком Retry-After.
public static class RateLimiting
{
    public const string Auth = "auth";
    public const string AiAnalysis = "ai-analysis";
    public const string PatientLookup = "patient-lookup";

    public static IServiceCollection AddHealthlyRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, _) =>
            {
                var http = context.HttpContext;
                var detail = "Too many requests. Please try again later.";
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    var seconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
                    http.Response.Headers.RetryAfter = seconds.ToString();
                    detail = seconds < 120
                        ? $"Too many requests. Please try again in {seconds} seconds."
                        : $"Too many requests. Please try again in {(int)Math.Ceiling(seconds / 60.0)} minutes.";
                }

                // Текст для сайта и приложения (ApiError показывает detail); traceId добавит ProblemDetailsDefaults
                await http.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = http,
                    ProblemDetails = { Status = StatusCodes.Status429TooManyRequests, Title = "Too Many Requests", Detail = detail },
                });
            };

            options.AddPolicy(Auth, context => FixedWindow(ByIp(context), permits: 20, TimeSpan.FromMinutes(1)));
            options.AddPolicy(AiAnalysis, context => FixedWindow(ByUser(context), permits: 20, TimeSpan.FromHours(1)));
            options.AddPolicy(PatientLookup, context => FixedWindow(ByUser(context), permits: 30, TimeSpan.FromMinutes(1)));
        });
        return services;
    }

    // IP клиента; за туннелем — настоящий адрес из X-Forwarded-For (Proxy:TrustForwardedHeaders в Program.cs)
    private static string ByIp(HttpContext context) =>
        "ip:" + (context.Connection.RemoteIpAddress?.ToString() ?? "unknown");

    // Авторизованный пользователь — по id (несколько человек за одним IP не мешают друг другу), иначе по IP
    private static string ByUser(HttpContext context) =>
        context.User.GetUserId() is { } userId ? "user:" + userId : ByIp(context);

    private static RateLimitPartition<string> FixedWindow(string key, int permits, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = window,
            QueueLimit = 0,
        });
}
