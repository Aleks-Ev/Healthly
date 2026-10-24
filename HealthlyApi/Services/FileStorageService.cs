namespace HealthlyApi.Services;

// Хранит загруженные файлы на диске в папке Storage:UploadsPath (по умолчанию HealthlyApi/uploads, не в git).
// В БД сохраняется только имя файла в хранилище — GUID + расширение, чтобы исключить
// подмену пути (../) и конфликты одинаковых имён.
public class FileStorageService
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 МБ

    private static readonly Dictionary<string, string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
    };

    private readonly string _root;

    public FileStorageService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var path = configuration["Storage:UploadsPath"];
        _root = Path.GetFullPath(string.IsNullOrWhiteSpace(path) ? "uploads" : path, environment.ContentRootPath);
        Directory.CreateDirectory(_root);
    }

    public static IReadOnlyCollection<string> AllowedExtensions => AllowedTypes.Keys;

    // MIME-тип по расширению или null, если расширение не разрешено
    public static string? GetContentType(string fileName) =>
        AllowedTypes.GetValueOrDefault(Path.GetExtension(fileName));

    public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var storedName = $"{Guid.NewGuid():N}{Path.GetExtension(file.FileName).ToLowerInvariant()}";

        await using var stream = File.Create(GetFullPath(storedName));
        await file.CopyToAsync(stream, cancellationToken);

        return storedName;
    }

    // Сохранить готовые байты (демо-данные в DbInitializer); extension — ".pdf", ".png" и т.д.
    public async Task<string> SaveAsync(byte[] content, string extension, CancellationToken cancellationToken = default)
    {
        var storedName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        await File.WriteAllBytesAsync(GetFullPath(storedName), content, cancellationToken);
        return storedName;
    }

    public Stream? OpenRead(string storedName)
    {
        var fullPath = GetFullPath(storedName);
        return File.Exists(fullPath) ? File.OpenRead(fullPath) : null;
    }

    public void Delete(string storedName)
    {
        var fullPath = GetFullPath(storedName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    // Path.GetFileName отбрасывает любые каталоги — файл всегда внутри _root
    private string GetFullPath(string storedName) => Path.Combine(_root, Path.GetFileName(storedName));
}
