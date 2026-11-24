using System.Text;
using HealthlyApi.Controllers;
using HealthlyApi.Data;
using HealthlyApi.Dtos;
using HealthlyApi.Middleware;
using HealthlyApi.Models;
using HealthlyApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace HealthlyApi.Tests;

// Кибербезопасность (Этап 8.2): блокировка аккаунта после неверных паролей, проверка файлов по содержимому,
// заголовки безопасности API

public class LoginLockoutTests
{
    private static readonly DateTime Now = new(2026, 11, 20, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Account_is_locked_on_the_fifth_wrong_password_in_a_row()
    {
        var user = new User();
        for (var i = 1; i < LoginLockout.MaxFailedAttempts; i++)
        {
            Assert.False(LoginLockout.RegisterFailure(user, Now));
            Assert.Null(LoginLockout.RemainingLockout(user, Now));
        }

        Assert.True(LoginLockout.RegisterFailure(user, Now));
        Assert.Equal(LoginLockout.LockoutDuration, LoginLockout.RemainingLockout(user, Now));
        Assert.Equal(TimeSpan.FromMinutes(5), LoginLockout.RemainingLockout(user, Now.AddMinutes(10)));
    }

    [Fact]
    public void Lock_expires_and_counting_starts_again()
    {
        var user = new User { LockoutEndsAt = Now.AddMinutes(-1), FailedLoginCount = 0 };

        Assert.Null(LoginLockout.RemainingLockout(user, Now));
        Assert.False(LoginLockout.RegisterFailure(user, Now)); // первая ошибка после блокировки не блокирует снова
        Assert.Equal(1, user.FailedLoginCount);
        Assert.Null(user.LockoutEndsAt);
    }

    [Fact]
    public void Successful_login_resets_the_counter()
    {
        var user = new User { FailedLoginCount = 3 };

        Assert.True(LoginLockout.Reset(user));
        Assert.Equal(0, user.FailedLoginCount);
        Assert.False(LoginLockout.Reset(user)); // уже чисто — сохранять нечего
    }

    [Theory]
    [InlineData(15, "15 minutes")]
    [InlineData(0.5, "1 minute.")]
    public void Message_tells_how_long_to_wait(double minutes, string expected)
    {
        Assert.Contains(expected, LoginLockout.Message(TimeSpan.FromMinutes(minutes)));
    }
}

public class LoginLockoutEndpointTests
{
    private const string Email = "elif@example.com";
    private const string Password = "Correct-Password-1";

    private static AuthController Controller(AppDbContext db)
    {
        var jwt = Options.Create(new JwtOptions { Issuer = "test", Audience = "test", Key = new string('k', 64) });
        var services = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider();
        return new AuthController(db, new PasswordService(), new TokenService(jwt), new RefreshTokenService(db, jwt, NullLogger<RefreshTokenService>.Instance))
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } },
        };
    }

    private static int AddPatient(ChatTestDatabase database)
    {
        var user = new User { Email = Email, FullName = "Elif Kaya", Role = Roles.Patient };
        user.PasswordHash = new PasswordService().Hash(user, Password);
        return database.AddUser(user).Id;
    }

    private static async Task<(int? Status, AuthController Controller)> LoginAsync(ChatTestDatabase database, string password)
    {
        await using var db = database.CreateContext();
        var controller = Controller(db);
        var result = await controller.Login(new LoginRequest { Email = Email, Password = password });
        return (result.Result is ObjectResult problem ? problem.StatusCode : StatusCodes.Status200OK, controller);
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_even_for_the_right_password()
    {
        using var database = new ChatTestDatabase();
        AddPatient(database);

        for (var i = 1; i < LoginLockout.MaxFailedAttempts; i++)
        {
            Assert.Equal(StatusCodes.Status401Unauthorized, (await LoginAsync(database, "wrong")).Status);
        }

        var (fifth, controller) = await LoginAsync(database, "wrong");
        Assert.Equal(StatusCodes.Status429TooManyRequests, fifth);
        Assert.Equal("900", controller.Response.Headers.RetryAfter.ToString()); // 15 минут в секундах

        // Правильный пароль во время блокировки не помогает — иначе подбор просто продолжался бы
        Assert.Equal(StatusCodes.Status429TooManyRequests, (await LoginAsync(database, Password)).Status);
    }

    [Fact]
    public async Task After_the_lock_expires_the_right_password_works_and_resets_the_counter()
    {
        using var database = new ChatTestDatabase();
        var id = AddPatient(database);
        await using (var db = database.CreateContext())
        {
            var user = await db.Users.FindAsync(id);
            user!.FailedLoginCount = 0;
            user.LockoutEndsAt = DateTime.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        await LoginAsync(database, "wrong"); // одна ошибка уже после блокировки

        Assert.Equal(StatusCodes.Status200OK, (await LoginAsync(database, Password)).Status);

        await using var check = database.CreateContext();
        var saved = await check.Users.FindAsync(id);
        Assert.Equal(0, saved!.FailedLoginCount);
        Assert.Null(saved.LockoutEndsAt);
    }

    [Fact]
    public async Task Unknown_email_gets_the_same_401_as_a_wrong_password()
    {
        using var database = new ChatTestDatabase();
        await using var db = database.CreateContext();

        var result = await Controller(db).Login(new LoginRequest { Email = "nobody@example.com", Password = "whatever-123" });

        var problem = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status401Unauthorized, problem.StatusCode);
        Assert.Equal("Invalid email or password", Assert.IsType<ProblemDetails>(problem.Value).Detail);
    }

    [Fact]
    public void Dummy_check_takes_as_long_as_a_real_one()
    {
        var passwords = new PasswordService();
        var user = new User();
        user.PasswordHash = passwords.Hash(user, Password);
        passwords.VerifyDummy("warm-up");
        passwords.Verify(user, "warm-up");

        var real = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 5; i++) passwords.Verify(user, "wrong");
        real.Stop();
        var dummy = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 5; i++) passwords.VerifyDummy("wrong");
        dummy.Stop();

        // Та же стоимость PBKDF2 — разница в разы, а не в десятки раз (грубая граница, чтобы тест не был хрупким)
        Assert.InRange(dummy.Elapsed.TotalMilliseconds, real.Elapsed.TotalMilliseconds / 3, real.Elapsed.TotalMilliseconds * 3);
    }
}

public class UploadSignatureTests
{
    private static IFormFile File(string name, byte[] content) =>
        new FormFile(new MemoryStream(content), 0, content.Length, "file", name);

    private static readonly byte[] Pdf = Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF");
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];
    private static readonly byte[] WindowsExe = [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00]; // «MZ» — программа Windows

    [Theory]
    [InlineData("report.pdf")]
    [InlineData("REPORT.PDF")]
    public async Task Real_pdf_is_accepted(string name)
    {
        Assert.Null(await FileStorageService.ValidateUploadAsync(File(name, Pdf), CancellationToken.None));
    }

    [Fact]
    public async Task Real_images_are_accepted()
    {
        Assert.Null(await FileStorageService.ValidateUploadAsync(File("scan.png", Png), CancellationToken.None));
        Assert.Null(await FileStorageService.ValidateUploadAsync(File("photo.jpg", Jpeg), CancellationToken.None));
        Assert.Null(await FileStorageService.ValidateUploadAsync(File("photo.jpeg", Jpeg), CancellationToken.None));
    }

    [Fact]
    public async Task Pdf_with_a_few_junk_bytes_before_the_header_is_accepted()
    {
        var content = Encoding.ASCII.GetBytes("\r\n\r\n").Concat(Pdf).ToArray();

        Assert.Null(await FileStorageService.ValidateUploadAsync(File("report.pdf", content), CancellationToken.None));
    }

    [Theory]
    [InlineData("results.pdf")]
    [InlineData("results.jpg")]
    [InlineData("results.png")]
    public async Task Renamed_program_is_rejected(string name)
    {
        var error = await FileStorageService.ValidateUploadAsync(File(name, WindowsExe), CancellationToken.None);

        Assert.Equal("The file content does not match its type. Upload a real PDF, JPG or PNG file.", error);
    }

    [Fact]
    public async Task Html_disguised_as_pdf_is_rejected()
    {
        var html = Encoding.UTF8.GetBytes("<html><script>alert(document.cookie)</script></html>");

        Assert.NotNull(await FileStorageService.ValidateUploadAsync(File("report.pdf", html), CancellationToken.None));
    }

    [Fact]
    public async Task Png_named_jpg_is_rejected()
    {
        Assert.NotNull(await FileStorageService.ValidateUploadAsync(File("photo.jpg", Png), CancellationToken.None));
    }

    [Fact]
    public async Task Extension_and_size_rules_still_apply()
    {
        Assert.Contains("Allowed file types", await FileStorageService.ValidateUploadAsync(File("virus.exe", WindowsExe), CancellationToken.None));
        Assert.Contains("between 1 byte and 10 MB", await FileStorageService.ValidateUploadAsync(File("empty.pdf", []), CancellationToken.None));
    }
}

public class SecurityHeadersTests
{
    private static IHeaderDictionary HeadersFor(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        SecurityHeadersMiddleware.Apply(context);
        return context.Response.Headers;
    }

    [Fact]
    public void Api_responses_get_strict_security_headers()
    {
        var headers = HeadersFor("/api/documents/42/file");

        Assert.Equal("nosniff", headers.XContentTypeOptions.ToString());
        Assert.Equal("DENY", headers.XFrameOptions.ToString());
        Assert.Equal("no-referrer", headers["Referrer-Policy"].ToString());
        Assert.Equal("same-site", headers["Cross-Origin-Resource-Policy"].ToString());
        Assert.StartsWith("default-src 'none'", headers.ContentSecurityPolicy.ToString());
        Assert.Contains("frame-ancestors 'none'", headers.ContentSecurityPolicy.ToString());
    }

    [Fact]
    public void Scalar_docs_page_keeps_working_without_the_strict_csp()
    {
        var headers = HeadersFor("/scalar/v1");

        Assert.Equal("nosniff", headers.XContentTypeOptions.ToString());
        Assert.False(headers.ContainsKey("Content-Security-Policy"));
    }
}
