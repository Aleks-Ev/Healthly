using HealthlyApi.Data;
using HealthlyApi.Middleware;
using HealthlyApi.OpenApi;
using HealthlyApi.Services;
using HealthlyApi.Services.Ai;
using HealthlyApi.Services.Ai.Ocr;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. Добавляем политику CORS для фронтенда на Next.js
// Адреса берутся из Cors:Origins (в Docker — переменные Cors__Origins__0, __1…), по умолчанию http://localhost:3000
var corsOrigins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() is { Length: > 0 } origins
    ? origins
    : ["http://localhost:3000"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(corsOrigins) // Разрешаем запросы с Next.js
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // браузер отправляет/принимает httpOnly-cookie с refresh-токеном (RefreshTokenCookie)
    });
});

// Подключаем AppDbContext с использованием Npgsql (PostgreSQL)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. JWT-аутентификация (Jwt:Key задаётся через dotnet user-secrets)
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();
if (jwtOptions.Key.Length < 32)
{
    throw new InvalidOperationException(
        "Jwt:Key is missing or shorter than 32 characters. Run: dotnet user-secrets set \"Jwt:Key\" \"<random string>\"");
}

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<RefreshTokenService>();
builder.Services.AddScoped<AuditLog>(); // журнал действий с документами (кто загрузил, кто подтвердил)
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<FileStorageService>();

// ИИ-анализ документов: Gemini (основной) → Groq (резервный), переключение — FallbackAiDocumentProcessor.
// Ключи — в user-secrets (Ai:Gemini:ApiKey, Ai:Groq:ApiKey), в Docker — GEMINI_API_KEY / GROQ_API_KEY в .env.
// Используются только провайдеры с ключом; без ключей остаётся заглушка MockAiDocumentProcessor,
// чтобы проект запускался и без доступа к внешнему ИИ.
builder.Services.Configure<AiOptions>(builder.Configuration.GetSection("Ai"));
// Текст документа — локально: PDF через PdfPig, фото и сканы через Tesseract (Ocr:TesseractPath), см. DocumentTextExtractor
builder.Services.Configure<OcrOptions>(builder.Configuration.GetSection("Ocr"));
builder.Services.AddSingleton<IOcrEngine, TesseractCliOcrEngine>();
builder.Services.AddScoped<DocumentTextExtractor>();
var aiOptions = builder.Configuration.GetSection("Ai").Get<AiOptions>() ?? new AiOptions();
var aiTimeout = TimeSpan.FromSeconds(aiOptions.TimeoutSeconds);
builder.Services.AddHttpClient<GeminiAiDocumentProcessor>(client => client.Timeout = aiTimeout);
builder.Services.AddHttpClient<GroqAiDocumentProcessor>(client => client.Timeout = aiTimeout);
builder.Services.AddScoped<IAiDocumentProcessor>(services =>
{
    var providers = new List<IAiDocumentProcessor>();
    if (aiOptions.Gemini.IsConfigured)
    {
        providers.Add(services.GetRequiredService<GeminiAiDocumentProcessor>());
    }
    if (aiOptions.Groq.IsConfigured)
    {
        providers.Add(services.GetRequiredService<GroqAiDocumentProcessor>());
    }
    return providers.Count == 0
        ? new MockAiDocumentProcessor()
        : new FallbackAiDocumentProcessor(providers, services.GetRequiredService<ILogger<FallbackAiDocumentProcessor>>());
});
builder.Services.AddScoped<DocumentAnalysisService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Оставляем claim'ы как есть ("sub", "role"), без маппинга в длинные ClaimTypes
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = jwtOptions.GetSigningKey(),
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();
// Лимиты на вход/регистрацию, ИИ-анализ и поиск по Healthly ID — см. Services/RateLimiting.cs
builder.Services.AddHealthlyRateLimiting();

// За Cloudflare Tunnel (и прокси Next.js) API получает запросы не от браузера, а от прокси.
// Proxy:TrustForwardedHeaders = true (в docker-compose.yml) → берём настоящие IP и схему (https) из X-Forwarded-*:
// IP нужен лимитам по адресу, https — флагу Secure у cookie с refresh-токеном.
// Доверяем любому отправителю, т.к. адреса прокси внутри Docker заранее неизвестны; локально (dotnet run) выключено
var trustForwardedHeaders = builder.Configuration.GetValue<bool>("Proxy:TrustForwardedHeaders");
if (trustForwardedHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddControllers(options => options.Filters.Add(new ProblemDetailsContentTypeFilter()));
// Все ошибки — в формате ProblemDetails (RFC 9457) с traceId; см. Middleware/ExceptionHandlingMiddleware.cs
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = ProblemDetailsDefaults.Customize);
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<ApiInfoTransformer>();
    options.AddDocumentTransformer<BearerSecurityTransformer>();
    options.AddOperationTransformer<BearerSecurityTransformer>();
});

var app = builder.Build();

// До всего остального: следующие middleware уже видят настоящие IP и схему запроса
if (trustForwardedHeaders)
{
    app.UseForwardedHeaders();
}

// Ловит исключения всех следующих middleware и контроллеров
app.UseMiddleware<ExceptionHandlingMiddleware>();
// Пустые ответы 400–599 (401 от JWT, 403, 404 неизвестного адреса) получают тело ProblemDetails
app.UseStatusCodePages();

// Миграции (если Database:MigrateOnStartup) и тестовые данные в Development — см. Data/DbInitializer.cs
await DbInitializer.InitializeAsync(app);

if (app.Environment.IsDevelopment())
{
    // OpenAPI JSON: /openapi/v1.json, интерактивная документация: /scalar
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "Healthly API";
        options.AddPreferredSecuritySchemes(BearerSecurityTransformer.SchemeName);
    });
}

// 3. Включаем CORS (ВАЖНО: ставить перед UseAuthentication/UseAuthorization и MapControllers)
app.UseCors("AllowReactApp");

// В Development мобильные клиенты ходят по http (эмулятор, телефон в той же Wi-Fi, ngrok),
// редирект на https://localhost сломал бы им запросы
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
// После аутентификации: лимиты ИИ-анализа считаются по пользователю
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

app.Run();
