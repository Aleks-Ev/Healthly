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

    // Сообщение об ошибке для клиента или null, если файл подходит (тип и размер) — одинаково для пациента и лаборатории
    public static string? ValidateUpload(IFormFile file)
    {
        if (GetContentType(file.FileName) == null)
        {
            return $"Allowed file types: {string.Join(", ", AllowedExtensions)}";
        }
        if (file.Length == 0 || file.Length > MaxFileSizeBytes)
        {
            return "File must be between 1 byte and 10 MB";
        }
        return null;
    }

    // Полная проверка загрузки: тип и размер (ValidateUpload) + содержимое файла (сигнатура, «магические байты»).
    // Расширение задаёт сам пользователь: «virus.exe», переименованный в «report.pdf», прошёл бы проверку по имени.
    // Поэтому читаем начало файла и сверяем с форматом, который обещает расширение
    public static async Task<string?> ValidateUploadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (ValidateUpload(file) is { } invalid)
        {
            return invalid;
        }

        var header = new byte[SignatureBytes];
        await using var stream = file.OpenReadStream();
        var read = 0;
        while (read < header.Length && await stream.ReadAsync(header.AsMemory(read), cancellationToken) is var count and > 0)
        {
            read += count;
        }

        return MatchesSignature(header.AsSpan(0, read), GetContentType(file.FileName)!)
            ? null
            : "The file content does not match its type. Upload a real PDF, JPG or PNG file.";
    }

    // Сколько байт с начала файла читаем: PDF допускает мусор до «%PDF-» в первых 1024 байтах (спецификация PDF)
    private const int SignatureBytes = 1024;

    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];

    // true — начало файла соответствует формату (MIME-типу из расширения)
    public static bool MatchesSignature(ReadOnlySpan<byte> header, string contentType) => contentType switch
    {
        "application/pdf" => header.IndexOf(PdfSignature) >= 0,
        "image/png" => header.StartsWith(PngSignature),
        "image/jpeg" => header.StartsWith(JpegSignature),
        _ => false,
    };

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
