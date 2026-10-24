namespace HealthlyApi.Services;

// Refresh-токен в httpOnly-cookie — для веб-клиента.
// JavaScript страницы такую cookie не видит, поэтому XSS не может украсть долгоживущий refresh-токен
// (в localStorage он был бы доступен любому скрипту на странице).
//
// Кто получает cookie: клиент, приславший заголовок X-Refresh-Token-Transport: cookie (веб-фронтенд),
// и любой, кто обновляет токены по cookie. Мобильные приложения заголовок не шлют и получают токен в JSON, как раньше.
public static class RefreshTokenCookie
{
    public const string Name = "healthly_refresh";
    public const string TransportHeader = "X-Refresh-Token-Transport";

    // Cookie уходит только на /api/auth/* (refresh, logout, смена пароля), а не с каждым запросом к API
    private const string CookiePath = "/api/auth";

    // Клиент попросил хранить refresh-токен в cookie
    public static bool IsRequested(HttpRequest request) =>
        string.Equals(request.Headers[TransportHeader], "cookie", StringComparison.OrdinalIgnoreCase);

    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(Name, out var token) && !string.IsNullOrEmpty(token) ? token : null;

    public static void Set(HttpContext context, string token, DateTime expiresAt) =>
        context.Response.Cookies.Append(Name, token, CreateOptions(context, expiresAt));

    public static void Delete(HttpContext context) =>
        context.Response.Cookies.Delete(Name, CreateOptions(context, null));

    private static CookieOptions CreateOptions(HttpContext context, DateTime? expiresAt) => new()
    {
        HttpOnly = true,
        // Secure (только HTTPS) — когда API открыт по HTTPS; локально по http браузер иначе не сохранил бы cookie
        Secure = context.Request.IsHttps,
        // Strict: браузер не отправит cookie с чужого сайта (защита от CSRF).
        // localhost:3000 и localhost:5097 — один «сайт» (порт не учитывается), поэтому фронтенду cookie приходит
        SameSite = SameSiteMode.Strict,
        Path = CookiePath,
        Expires = expiresAt,
        IsEssential = true,
    };
}
