using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using HealthlyApi.Models;
using HealthlyApi.Services.Ai;

namespace HealthlyApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<MedicalDocument> MedicalDocuments => Set<MedicalDocument>();
    public DbSet<TestResult> TestResults => Set<TestResult>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ReferenceMedicalValue> ReferenceMedicalValues => Set<ReferenceMedicalValue>();
    public DbSet<DocumentAnalysis> DocumentAnalyses => Set<DocumentAnalysis>();
    public DbSet<Laboratory> Laboratories => Set<Laboratory>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<CareConnection> CareConnections => Set<CareConnection>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(user =>
        {
            user.HasIndex(u => u.Email).IsUnique();
            user.Property(u => u.Email).HasMaxLength(256);
            user.Property(u => u.FullName).HasMaxLength(200);
            user.Property(u => u.Role).HasMaxLength(20);
            user.Property(u => u.Gender).HasMaxLength(50);
            user.Property(u => u.PhoneNumber).HasMaxLength(20);
            user.Property(u => u.City).HasMaxLength(50);
            user.Property(u => u.AccessCode).HasMaxLength(50);
        });

        modelBuilder.Entity<MedicalDocument>(document =>
        {
            document.Property(d => d.OriginalFileName).HasMaxLength(255);
            document.Property(d => d.FilePath).HasMaxLength(100);
            document.Property(d => d.ContentType).HasMaxLength(100);
            document.Property(d => d.Status).HasMaxLength(20);
        });

        modelBuilder.Entity<RefreshToken>(token =>
        {
            // Поиск токена при обновлении — по хэшу
            token.HasIndex(t => t.TokenHash).IsUnique();
            token.Property(t => t.TokenHash).HasMaxLength(64);
            token.Property(t => t.ReplacedByTokenHash).HasMaxLength(64);
            // Удаление пользователя удаляет и его токены
            token.HasOne(t => t.User).WithMany().HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReferenceMedicalValue>(value =>
        {
            // Не уникальный: у показателя может быть несколько строк (женщины/мужчины, возрастные группы)
            value.HasIndex(v => v.MetricName);
            value.Property(v => v.MetricName).HasMaxLength(100);
            value.Property(v => v.Aliases).HasMaxLength(500);
            value.Property(v => v.Sex).HasMaxLength(10);
            // numeric(10,3): до 9 999 999.999 — хватает и для g/L, и для 10^9/L
            value.Property(v => v.MinNormal).HasPrecision(10, 3);
            value.Property(v => v.MaxNormal).HasPrecision(10, 3);
            value.Property(v => v.CriticalLow).HasPrecision(10, 3);
            value.Property(v => v.CriticalHigh).HasPrecision(10, 3);
            value.Property(v => v.Unit).HasMaxLength(30);
            value.Property(v => v.Category).HasMaxLength(100);
            value.Property(v => v.DoctorSpecialty).HasMaxLength(100);
            value.Property(v => v.Source).HasMaxLength(300);
            value.Property(v => v.ReviewedBy).HasMaxLength(100);
            // Строки не создаются миграцией: их при каждом запуске синхронизирует DbInitializer
            // из Data/reference-values.csv (файл правят врачи-консультанты, см. Data/REFERENCE_VALUES.md)
        });

        modelBuilder.Entity<TestResult>(result =>
        {
            result.Property(r => r.TestName).HasMaxLength(100);
            result.Property(r => r.Value).HasMaxLength(50);
            result.Property(r => r.Unit).HasMaxLength(30);
            result.Property(r => r.ReferenceRange).HasMaxLength(50);
        });

        modelBuilder.Entity<Laboratory>(laboratory =>
        {
            laboratory.Property(l => l.Name).HasMaxLength(200);
            laboratory.Property(l => l.City).HasMaxLength(50);
            laboratory.Property(l => l.Address).HasMaxLength(300);
            laboratory.Property(l => l.Description).HasMaxLength(600);
            laboratory.Property(l => l.OpeningHours).HasMaxLength(100);
            laboratory.Property(l => l.Phone).HasMaxLength(20);
        });

        // Healthly ID уникален; лабораторию удалили — сотрудник и документы остаются, связь обнуляется
        modelBuilder.Entity<User>(user =>
        {
            user.HasIndex(u => u.PatientCode).IsUnique();
            user.Property(u => u.PatientCode).HasMaxLength(20);
            user.HasOne(u => u.Laboratory).WithMany().HasForeignKey(u => u.LaboratoryId).OnDelete(DeleteBehavior.SetNull);
            // Профиль врача
            user.Property(u => u.Specialty).HasMaxLength(100);
            user.Property(u => u.Workplace).HasMaxLength(200);
            user.Property(u => u.Bio).HasMaxLength(600);
            user.Property(u => u.Languages).HasMaxLength(100);
            // Значение по умолчанию true для старых строк; HasSentinel(true) — чтобы явное false при создании не подменялось умолчанием БД
            user.Property(u => u.AcceptingPatients).HasDefaultValue(true).HasSentinel(true);
        });

        modelBuilder.Entity<Laboratory>().Property(l => l.AcceptingPatients).HasDefaultValue(true).HasSentinel(true);

        // Связи пациент ↔ врач / лаборатория. Удалили любую сторону — связь удаляется вместе с ней.
        // Частичные уникальные индексы: с одним врачом (лабораторией) у пациента не больше одной «открытой» связи
        modelBuilder.Entity<CareConnection>(connection =>
        {
            connection.HasOne(c => c.Patient).WithMany().HasForeignKey(c => c.PatientId).OnDelete(DeleteBehavior.Cascade);
            connection.HasOne(c => c.Doctor).WithMany().HasForeignKey(c => c.DoctorId).OnDelete(DeleteBehavior.Cascade);
            connection.HasOne(c => c.Laboratory).WithMany().HasForeignKey(c => c.LaboratoryId).OnDelete(DeleteBehavior.Cascade);
            connection.Property(c => c.ProviderType).HasMaxLength(20);
            connection.Property(c => c.Status).HasMaxLength(20);
            connection.Property(c => c.PatientMessage).HasMaxLength(300);
            connection.Property(c => c.ProviderNote).HasMaxLength(300);
            connection.HasIndex(c => new { c.PatientId, c.DoctorId })
                .IsUnique()
                .HasFilter("\"DoctorId\" IS NOT NULL AND \"Status\" IN ('Requested', 'Accepted', 'Active')");
            connection.HasIndex(c => new { c.PatientId, c.LaboratoryId })
                .IsUnique()
                .HasFilter("\"LaboratoryId\" IS NOT NULL AND \"Status\" IN ('Requested', 'Accepted', 'Active')");
            connection.HasIndex(c => new { c.DoctorId, c.Status });
            connection.HasIndex(c => new { c.LaboratoryId, c.Status });
            // Ровно одна сторона-«поставщик»: врач или лаборатория
            connection.ToTable(t => t.HasCheckConstraint(
                "CK_CareConnections_Provider",
                "(\"DoctorId\" IS NOT NULL AND \"LaboratoryId\" IS NULL) OR (\"DoctorId\" IS NULL AND \"LaboratoryId\" IS NOT NULL)"));
        });

        // ИИ-чат: история пользователя читается по (UserId, CreatedAt); удалили пользователя — удалилась переписка
        modelBuilder.Entity<ChatMessage>(message =>
        {
            message.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
            message.HasIndex(m => new { m.UserId, m.CreatedAt });
            message.Property(m => m.Role).HasMaxLength(20);
            message.Property(m => m.Content).HasMaxLength(8000);
        });

        modelBuilder.Entity<Notification>(notification =>
        {
            notification.HasOne(n => n.User).WithMany().HasForeignKey(n => n.UserId).OnDelete(DeleteBehavior.Cascade);
            notification.HasOne(n => n.CareConnection).WithMany().HasForeignKey(n => n.CareConnectionId).OnDelete(DeleteBehavior.SetNull);
            notification.HasIndex(n => new { n.UserId, n.ReadAt });
            notification.Property(n => n.Type).HasMaxLength(50);
            notification.Property(n => n.ActorName).HasMaxLength(200);
            notification.Property(n => n.Title).HasMaxLength(200);
            notification.Property(n => n.Body).HasMaxLength(500);
            notification.Property(n => n.Link).HasMaxLength(200);
        });
        modelBuilder.Entity<MedicalDocument>(document =>
        {
            document.HasIndex(d => d.LaboratoryId);
            document.HasOne(d => d.Laboratory).WithMany().HasForeignKey(d => d.LaboratoryId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<AuditEvent>(audit =>
        {
            audit.HasIndex(a => a.DocumentId);
            audit.HasIndex(a => a.PatientId);
            audit.Property(a => a.UserName).HasMaxLength(200);
            audit.Property(a => a.UserRole).HasMaxLength(20);
            audit.Property(a => a.Action).HasMaxLength(50);
            audit.Property(a => a.Details).HasMaxLength(500);
        });

        modelBuilder.Entity<DocumentAnalysis>(analysis =>
        {
            // Один анализ на документ; удалили документ — удалился и анализ
            analysis.HasIndex(a => a.MedicalDocumentId).IsUnique();
            analysis.HasOne(a => a.MedicalDocument)
                .WithOne()
                .HasForeignKey<DocumentAnalysis>(a => a.MedicalDocumentId)
                .OnDelete(DeleteBehavior.Cascade);
            analysis.Property(a => a.Model).HasMaxLength(100);
            // Показатели от ИИ — одним jsonb-полем: это снимок ответа модели, отдельная таблица не нужна
            analysis.Property(a => a.Indicators)
                .HasColumnType("jsonb")
                .HasConversion(
                    indicators => JsonSerializer.Serialize(indicators, JsonOptions),
                    json => JsonSerializer.Deserialize<List<AiExtractedIndicator>>(json, JsonOptions) ?? new List<AiExtractedIndicator>(),
                    new ValueComparer<List<AiExtractedIndicator>>(
                        (a, b) => a!.SequenceEqual(b!),
                        list => list.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                        list => list.ToList()));
        });
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
