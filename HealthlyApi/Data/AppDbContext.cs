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
