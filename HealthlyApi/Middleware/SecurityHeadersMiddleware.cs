namespace HealthlyApi.Middleware;

// Заголовки безопасности на каждом ответе API (сайт ставит свои — healthly-web/next.config.ts).
// API отдаёт только JSON и файлы анализов, поэтому политика самая строгая:
//   X-Content-Type-Options: nosniff  — браузер не «угадывает» тип: загруженный файл не выполнится как HTML/скрипт;
//   X-Frame-Options: DENY и CSP frame-ancestors 'none' — ответ API нельзя встроить в чужую страницу (clickjacking);
//   Content-Security-Policy: default-src 'none' — если ответ всё же откроют как страницу, ничего не загрузится и не выполнится;
//   Referrer-Policy: no-referrer — адрес запроса (с id документа) не уходит третьим сайтам;
//   Cross-Origin-Resource-Policy: same-site — файлы анализов нельзя подключить с чужого сайта как <img>/<script>.
// Страница документации /scalar (только Development) грузит скрипты и стили — для неё CSP не ставим.
// Заголовки добавляются в OnStarting: ExceptionHandlingMiddleware при ошибке вызывает Response.Clear(), который стёр бы
// заголовки, выставленные заранее, — а так они попадают и в ответы с ошибками.
// HSTS («только HTTPS») для API включает app.UseHsts() в Program.cs (вне Development).
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            Apply(context);
            return Task.CompletedTask;
        });
        return _next(context);
    }

    public static void Apply(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Cross-Origin-Resource-Policy"] = "same-site";
        headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
        if (!context.Request.Path.StartsWithSegments("/scalar"))
        {
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";
        }
    }
}
