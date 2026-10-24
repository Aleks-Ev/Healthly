using System.Text;
using Microsoft.EntityFrameworkCore;
using HealthlyApi.Dtos;
using HealthlyApi.Models;
using HealthlyApi.Services;

namespace HealthlyApi.Data;

// Подготовка базы при запуске API (вызывается из Program.cs):
// 1. Database:MigrateOnStartup = true → применить миграции (включено в Docker; локально — dotnet ef database update).
// 2. Только в Development:
//    - администратор из user-secrets (SeedAdmin:Email / SeedAdmin:Password), если его ещё нет;
//    - DemoData:Enabled = true → три демо-аккаунта с историей анализов за последние 6 месяцев
//      (глюкоза, холестерин, гемоглобин), чтобы графики Health Trends сразу показывали динамику.
//
// Правила для демо-данных (ничего не ломаем):
//    - аккаунт создаётся, только если email свободен; пароль существующего аккаунта не меняется;
//    - история добавляется, только если у аккаунта нет ни одного документа;
//    - DemoData:ResetHistory = true → удалить документы демо-аккаунтов и создать историю заново
//      (даты считаются от сегодняшнего дня, поэтому так можно «освежить» графики).
public static class DbInitializer
{
    // Пароли демо-аккаунтов — только для локальной разработки (в Production сидинг не запускается)
    public const string PatientEmail = "patient@healthly.com";
    public const string PatientPassword = "Patient123!";
    public const string DoctorEmail = "doctor@healthly.com";
    public const string DoctorPassword = "Doctor123!";
    public const string AdminEmail = "admin@healthly.com";
    public const string AdminPassword = "Admin123!";

    // Сколько месяцев истории и сколько дней назад был последний анализ
    private const int HistoryMonths = 6;
    private const int LatestVisitDaysAgo = 7;

    // Референсные диапазоны показателей (взрослые)
    private const string GlucoseRange = "3.9-5.6";       // ммоль/л, натощак
    private const string CholesterolRange = "<5.2";      // ммоль/л, общий
    private const string HemoglobinFemaleRange = "120-150";
    private const string HemoglobinMaleRange = "130-170"; // г/л

    // Профиль демо-аккаунта и его анализы: значения от самого старого к самому свежему (по одному в месяц)
    private record DemoAccount(
        User User,
        string Password,
        decimal[] Glucose,
        decimal[] Cholesterol,
        decimal[] Hemoglobin,
        string HemoglobinRange,
        bool AddPendingDocument);

    public static async Task InitializeAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<AppDbContext>();

        if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
        {
            await context.Database.MigrateAsync();
        }

        if (!app.Environment.IsDevelopment())
        {
            return;
        }

        var passwords = services.GetRequiredService<PasswordService>();
        await SeedAdminFromSecretsAsync(app, context, passwords);

        if (app.Configuration.GetValue<bool>("DemoData:Enabled"))
        {
            var storage = services.GetRequiredService<FileStorageService>();
            var resetHistory = app.Configuration.GetValue<bool>("DemoData:ResetHistory");
            await SeedDemoAccountsAsync(app.Logger, context, passwords, storage, resetHistory);
        }
    }

    private static async Task SeedAdminFromSecretsAsync(WebApplication app, AppDbContext context, PasswordService passwords)
    {
        var email = app.Configuration["SeedAdmin:Email"];
        var password = app.Configuration["SeedAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        email = UserMappings.NormalizeEmail(email);

        if (await context.Users.AnyAsync(u => u.Email == email))
        {
            return;
        }

        var admin = new User { Email = email, FullName = "Administrator", Role = Roles.Admin };
        admin.PasswordHash = passwords.Hash(admin, password);

        context.Users.Add(admin);
        await context.SaveChangesAsync();

        app.Logger.LogInformation("Admin user created: {Email}", email);
    }

    private static async Task SeedDemoAccountsAsync(
        ILogger logger, AppDbContext context, PasswordService passwords, FileStorageService storage, bool resetHistory)
    {
        DemoAccount[] accounts =
        [
            // Пациентка: сахар и холестерин выше нормы, гемоглобин ниже — к последнему анализу всё приходит в норму
            new(
                new User
                {
                    Email = PatientEmail,
                    FullName = "Elif Kaya",
                    Role = Roles.Patient,
                    Gender = Genders.Female,
                    Height = 165,
                    Weight = 60,
                    PhoneNumber = "+905321112233",
                    DateOfBirth = new DateOnly(1992, 4, 18),
                    City = TestingCities.Istanbul,
                    ConsentsAcceptedAt = DateTime.UtcNow,
                },
                PatientPassword,
                Glucose: [6.3m, 6.0m, 6.1m, 5.7m, 5.3m, 5.0m],
                Cholesterol: [6.2m, 6.0m, 5.7m, 5.5m, 5.3m, 4.9m],
                Hemoglobin: [116m, 121m, 119m, 127m, 131m, 136m],
                HemoglobinFemaleRange,
                AddPendingDocument: true),

            // Врач: стабильные показатели в пределах нормы
            new(
                new User
                {
                    Email = DoctorEmail,
                    FullName = "Dr. Mehmet Yilmaz",
                    Role = Roles.Doctor,
                    Gender = Genders.Male,
                    Height = 180,
                    Weight = 82,
                    PhoneNumber = "+905332223344",
                    DateOfBirth = new DateOnly(1981, 9, 3),
                    City = TestingCities.Ankara,
                    ConsentsAcceptedAt = DateTime.UtcNow,
                },
                DoctorPassword,
                Glucose: [4.9m, 5.1m, 4.8m, 5.0m, 5.2m, 4.9m],
                Cholesterol: [4.6m, 4.8m, 4.7m, 4.5m, 4.9m, 4.7m],
                Hemoglobin: [148m, 151m, 146m, 150m, 149m, 152m],
                HemoglobinMaleRange,
                AddPendingDocument: false),

            // Администратор: холестерин постепенно растёт и выходит за норму — пример ухудшения
            new(
                new User
                {
                    Email = AdminEmail,
                    FullName = "Healthly Admin",
                    Role = Roles.Admin,
                    Gender = Genders.Other,
                    PhoneNumber = "+905344445566",
                    DateOfBirth = new DateOnly(1988, 12, 11),
                    City = TestingCities.Izmir,
                    ConsentsAcceptedAt = DateTime.UtcNow,
                },
                AdminPassword,
                Glucose: [5.2m, 5.4m, 5.3m, 5.6m, 5.8m, 5.9m],
                Cholesterol: [4.8m, 5.0m, 5.1m, 5.4m, 5.6m, 5.9m],
                Hemoglobin: [138m, 140m, 137m, 139m, 141m, 138m],
                HemoglobinMaleRange,
                AddPendingDocument: false),
        ];

        var seeded = new List<string>();
        var filesToDelete = new List<string>();

        foreach (var account in accounts)
        {
            var user = await context.Users.FirstOrDefaultAsync(u => u.Email == account.User.Email);
            var documents = user == null
                ? []
                : await context.MedicalDocuments.Where(d => d.UserId == user.Id).ToListAsync();

            if (user == null)
            {
                user = account.User;
                user.PasswordHash = passwords.Hash(user, account.Password);
                context.Users.Add(user);
            }
            else if (resetHistory && documents.Count > 0)
            {
                // Показатели удаляются каскадно вместе с документами; файлы с диска — после успешного сохранения
                filesToDelete.AddRange(documents.Select(d => d.FilePath));
                context.MedicalDocuments.RemoveRange(documents);
                documents.Clear();
            }

            if (documents.Count == 0)
            {
                await AddHistoryAsync(context, storage, user, account);
                seeded.Add(user.Email);
            }
        }

        await context.SaveChangesAsync();

        foreach (var file in filesToDelete)
        {
            storage.Delete(file);
        }

        if (seeded.Count > 0)
        {
            logger.LogInformation("Demo lab history created for: {Emails}", string.Join(", ", seeded));
        }
    }

    // Один документ (бланк анализов) в месяц: самый свежий — LatestVisitDaysAgo дней назад
    private static async Task AddHistoryAsync(AppDbContext context, FileStorageService storage, User user, DemoAccount account)
    {
        var latestVisit = DateTime.UtcNow.Date.AddDays(-LatestVisitDaysAgo).AddHours(9);

        for (var i = 0; i < HistoryMonths; i++)
        {
            var monthsAgo = HistoryMonths - 1 - i;
            var visitDate = latestVisit.AddMonths(-monthsAgo);

            var document = await CreateDocumentAsync(storage, user, $"lab-results-{visitDate:yyyy-MM-dd}.pdf", visitDate);
            document.Status = DocumentStatuses.Completed;
            document.TestResults =
            [
                CreateResult("Glucose", account.Glucose[i], "mmol/L", GlucoseRange),
                CreateResult("Cholesterol", account.Cholesterol[i], "mmol/L", CholesterolRange),
                CreateResult("Hemoglobin", account.Hemoglobin[i], "g/L", account.HemoglobinRange),
            ];
            context.MedicalDocuments.Add(document);
        }

        // Документ без показателей, ждёт проверки врачом — для экрана модерации
        if (account.AddPendingDocument)
        {
            var uploadDate = DateTime.UtcNow.Date.AddDays(-1).AddHours(9);
            context.MedicalDocuments.Add(await CreateDocumentAsync(storage, user, $"thyroid-panel-{uploadDate:yyyy-MM-dd}.pdf", uploadDate));
        }
    }

    private static async Task<MedicalDocument> CreateDocumentAsync(FileStorageService storage, User user, string fileName, DateTime uploadDate)
    {
        var content = CreatePdf($"Healthly demo lab report: {fileName}");
        return new MedicalDocument
        {
            User = user,
            OriginalFileName = fileName,
            FilePath = await storage.SaveAsync(content, ".pdf"),
            ContentType = "application/pdf",
            FileSizeBytes = content.Length,
            UploadDate = uploadDate,
            Status = DocumentStatuses.Pending,
        };
    }

    private static TestResult CreateResult(string name, decimal value, string unit, string referenceRange)
    {
        // Значения в БД — строки ("5.4"); InvariantCulture, чтобы на русской Windows не получилось "5,4"
        var text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new TestResult
        {
            TestName = name,
            Value = text,
            Unit = unit,
            ReferenceRange = referenceRange,
            IsAbnormal = ReferenceRangeEvaluator.IsAbnormal(text, referenceRange) ?? false,
        };
    }

    // Минимальный корректный PDF с одной строкой текста (чтобы скачанный документ открывался в просмотрщике)
    private static byte[] CreatePdf(string text)
    {
        var stream = $"BT /F1 14 Tf 72 760 Td ({text}) Tj ET";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {stream.Length} >>\nstream\n{stream}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ];

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xrefOffset = pdf.Length;
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            pdf.Append($"{offset:D10} 00000 n \n");
        }
        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");

        // Только ASCII, поэтому длина строки = числу байтов и смещения в xref верны
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }
}
