using HealthlyApi.Data;
using HealthlyApi.Middleware;
using HealthlyApi.OpenApi;
using HealthlyApi.Services;
using HealthlyApi.Services.Ai;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<FileStorageService>();

// ИИ-анализ документов. Сейчас — заглушка; для реального OCR + LLM достаточно зарегистрировать здесь
// другую реализацию IAiDocumentProcessor (ключи API — в user-secrets, как Jwt:Key). Остальной код не меняется.
builder.Services.AddScoped<IAiDocumentProcessor, MockAiDocumentProcessor>();
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

// Первым в конвейере — ловит исключения всех следующих middleware и контроллеров
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
app.UseAuthorization();
app.MapControllers();

app.Run();
