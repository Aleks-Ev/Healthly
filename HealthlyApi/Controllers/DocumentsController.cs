using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using HealthlyApi.Data;
using HealthlyApi.Dtos;
using HealthlyApi.Models;
using HealthlyApi.Services;
using HealthlyApi.Services.Ai;

namespace HealthlyApi.Controllers;

// Медицинские документы (файлы анализов). Доступ: владелец, Doctor, Admin.
[ApiController]
[Route("api/documents")]
[Authorize]
[Produces("application/json")]
public class DocumentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly FileStorageService _storage;
    private readonly DocumentAnalysisService _analysis;
    private readonly AuditLog _audit;

    public DocumentsController(AppDbContext context, FileStorageService storage, DocumentAnalysisService analysis, AuditLog audit)
    {
        _context = context;
        _storage = storage;
        _analysis = analysis;
        _audit = audit;
    }

    /// <summary>List a user's documents</summary>
    /// <remarks>Newest first. Allowed for the owner, Doctor and Admin.</remarks>
    /// <response code="200">The documents, with result and out-of-range counts.</response>
    /// <response code="403">A patient requested another user's documents.</response>
    [HttpGet("/api/users/{userId:int}/documents")]
    [ProducesResponseType<IEnumerable<DocumentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<DocumentResponse>>> GetUserDocuments(int userId)
    {
        if (!User.CanAccessUser(userId))
        {
            return Forbid();
        }

        return await _context.MedicalDocuments
            .AsNoTracking()
            .Where(d => d.UserId == userId)
            .OrderByDescending(d => d.UploadDate)
            .Select(d => new DocumentResponse(
                d.Id,
                d.UserId,
                d.OriginalFileName,
                d.ContentType,
                d.FileSizeBytes,
                d.UploadDate,
                d.Status,
                d.TestResults.Count,
                d.TestResults.Count(r => r.IsAbnormal)))
            .ToListAsync();
    }

    /// <summary>Upload a document (PDF, PNG or JPG, up to 10 MB)</summary>
    /// <remarks>
    /// <c>multipart/form-data</c> with a single field named <c>file</c>. The new document has status <c>Pending</c> and no test results;
    /// results are added with <c>POST /api/documents/{documentId}/results</c>. Allowed for the owner, Doctor and Admin.
    /// </remarks>
    /// <response code="201">Document uploaded.</response>
    /// <response code="400">No file, a wrong file type or a file over 10 MB.</response>
    /// <response code="403">A patient tried to upload to another user's records.</response>
    /// <response code="404">User not found.</response>
    [HttpPost("/api/users/{userId:int}/documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(FileStorageService.MaxFileSizeBytes + 64 * 1024)]
    [ProducesResponseType<DocumentDetailsResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDetailsResponse>> Upload(int userId, IFormFile file, CancellationToken cancellationToken)
    {
        if (!User.CanAccessUser(userId))
        {
            return Forbid();
        }

        if (!await _context.Users.AnyAsync(u => u.Id == userId, cancellationToken))
        {
            return Problem(detail: "User not found", statusCode: StatusCodes.Status404NotFound);
        }

        if (FileStorageService.ValidateUpload(file) is { } invalid)
        {
            return Problem(detail: invalid, statusCode: StatusCodes.Status400BadRequest);
        }

        var document = new MedicalDocument
        {
            UserId = userId,
            UploadedByUserId = User.GetUserId(),
            OriginalFileName = Path.GetFileName(file.FileName),
            FilePath = await _storage.SaveAsync(file, cancellationToken),
            ContentType = FileStorageService.GetContentType(file.FileName)!,
            FileSizeBytes = file.Length,
        };

        _context.MedicalDocuments.Add(document);
        await _context.SaveChangesAsync(cancellationToken);
        _audit.Add(User, AuditActions.DocumentUploaded, document.Id, userId, document.OriginalFileName);
        await _context.SaveChangesAsync(cancellationToken);

        return CreatedAtAction(nameof(GetDocument), new { id = document.Id }, document.ToDetailsResponse());
    }

    /// <summary>Get a document with its test results</summary>
    /// <response code="200">The document and its results.</response>
    /// <response code="403">A patient requested another user's document.</response>
    /// <response code="404">Document not found.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<DocumentDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDetailsResponse>> GetDocument(int id)
    {
        var document = await _context.MedicalDocuments
            .AsNoTracking()
            .Include(d => d.TestResults)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (document == null)
        {
            return DocumentNotFound();
        }

        if (!User.CanAccessDocument(document))
        {
            return Forbid();
        }

        return document.ToDetailsResponse();
    }

    /// <summary>Download the original file</summary>
    /// <remarks>Returns the file bytes with its original <c>Content-Type</c> and file name (<c>Content-Disposition</c>).</remarks>
    /// <response code="200">The file.</response>
    /// <response code="403">A patient requested another user's file.</response>
    /// <response code="404">Document not found, or the file is missing from storage.</response>
    [HttpGet("{id:int}/file")]
    [Produces("application/octet-stream")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadFile(int id)
    {
        var document = await _context.MedicalDocuments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);

        if (document == null)
        {
            return DocumentNotFound();
        }

        if (!User.CanAccessDocument(document))
        {
            return Forbid();
        }

        var stream = _storage.OpenRead(document.FilePath);

        if (stream == null)
        {
            return Problem(detail: "File is missing from storage", statusCode: StatusCodes.Status404NotFound);
        }

        return File(stream, document.ContentType, document.OriginalFileName);
    }

    /// <summary>Analyze a document with AI</summary>
    /// <remarks>
    /// Recognizes the lab report, masks personal data, extracts the indicators and writes a plain-language summary.
    /// Each indicator is compared with the reference norms for the patient's sex and age (<c>isAbnormal</c>, <c>isCritical</c>,
    /// <c>category</c>, <c>doctorSpecialty</c>); <c>suggestedSpecialists</c> lists the doctors for the out-of-range ones.
    ///
    /// The text is read on the server (the PDF text layer, or OCR for photos and scans) and anonymized before it is sent to the AI model
    /// (Google Gemini, with Groq as an automatic fallback); <c>model</c> shows which one answered, e.g. <c>"gemini:gemini-3.5-flash"</c>.
    /// The AI only extracts the values; whether a value is normal is decided by the reference norms.
    /// A low <c>confidence</c> (≤ 0.4) means the value was hard to read in the photo: ask the user to check it against the original.
    /// When no AI keys are configured on the server, a mock is used (<c>isMock: true</c>, <c>model: "mock-ai-v1"</c>).
    ///
    /// The result is saved as a **draft** (<c>confirmedAt: null</c>). Calling this again returns the saved draft without a new
    /// AI request; <c>?force=true</c> analyzes the file again and resets the confirmation. The values reach the history and charts
    /// only after <c>POST /api/documents/{id}/analysis/confirm</c>. Allowed for the owner, Doctor, Admin and staff of the laboratory that uploaded it.
    /// </remarks>
    /// <param name="id">Document id.</param>
    /// <param name="force">Analyze the file again even if a saved analysis exists (uses the AI quota).</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">The analysis (saved draft or new).</response>
    /// <response code="403">A patient tried to analyze another user's document.</response>
    /// <response code="404">Document not found, or its file is missing from storage.</response>
    /// <response code="422">No text could be read from the document: a blurry or dark photo, a damaged file. The detail says what to do.</response>
    /// <response code="429">More than 20 analyze requests in an hour by this user; wait for the time in <c>Retry-After</c> (seconds).</response>
    /// <response code="503">All AI providers or the text recognition are unavailable; try again later.</response>
    [HttpPost("{id:int}/analyze")]
    [EnableRateLimiting(RateLimiting.AiAnalysis)]
    [ProducesResponseType<DocumentAnalysisResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DocumentAnalysisResponse>> Analyze(int id, CancellationToken cancellationToken, [FromQuery] bool force = false)
    {
        var (document, error) = await FindAccessibleDocumentAsync(id, cancellationToken);
        if (document == null)
        {
            return error!;
        }

        // В журнал — только настоящий вызов ИИ (сохранённый черновик просто отдаётся)
        var isNewAnalysis = force || !await _context.DocumentAnalyses.AnyAsync(a => a.MedicalDocumentId == id, cancellationToken);
        var result = await _analysis.AnalyzeAsync(document, document.User!, force, cancellationToken);

        if (result == null)
        {
            return Problem(detail: "File is missing from storage", statusCode: StatusCodes.Status404NotFound);
        }

        if (isNewAnalysis)
        {
            _audit.Add(User, AuditActions.DocumentAnalyzed, id, document.UserId, result.Model);
            await _context.SaveChangesAsync(cancellationToken);
        }
        return result;
    }

    /// <summary>Get the saved AI analysis of a document</summary>
    /// <remarks>
    /// The draft or confirmed analysis created by <c>POST /api/documents/{id}/analyze</c>. Normal / out of range / critical is
    /// evaluated again on every request with the current reference norms. Allowed for the owner, Doctor, Admin and the uploading lab's staff.
    /// </remarks>
    /// <response code="200">The saved analysis.</response>
    /// <response code="403">A patient requested another user's document.</response>
    /// <response code="404">Document not found, or it has not been analyzed yet.</response>
    [HttpGet("{id:int}/analysis")]
    [ProducesResponseType<DocumentAnalysisResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentAnalysisResponse>> GetAnalysis(int id, CancellationToken cancellationToken)
    {
        var (document, error) = await FindAccessibleDocumentAsync(id, cancellationToken);
        if (document == null)
        {
            return error!;
        }

        var result = await _analysis.GetAsync(document, document.User!, cancellationToken);
        return result == null
            ? Problem(detail: "This document has not been analyzed yet", statusCode: StatusCodes.Status404NotFound)
            : result;
    }

    /// <summary>Confirm the AI analysis and save the indicators</summary>
    /// <remarks>
    /// Send the indicators after checking them against the original report (fix misread values, remove wrong rows).
    /// They **replace all test results of the document**: values are converted to the reference units (e.g. hemoglobin
    /// 12 g/dL → 120 g/L) so the history and charts use one unit, then <c>isAbnormal</c> / <c>isCritical</c> are set by the norms.
    /// The document becomes <c>Completed</c> and the analysis gets <c>confirmedAt</c>. Allowed for the owner, Doctor, Admin and the uploading lab's staff.
    /// </remarks>
    /// <response code="200">The document with its saved test results.</response>
    /// <response code="400">Validation failed; see <c>errors</c>.</response>
    /// <response code="403">A patient tried to confirm another user's document.</response>
    /// <response code="404">Document not found, or it has not been analyzed yet.</response>
    [HttpPost("{id:int}/analysis/confirm")]
    [ProducesResponseType<DocumentDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDetailsResponse>> ConfirmAnalysis(int id, ConfirmAnalysisRequest request, CancellationToken cancellationToken)
    {
        var (document, error) = await FindAccessibleDocumentAsync(id, cancellationToken);
        if (document == null)
        {
            return error!;
        }

        // Запись в журнал сохраняется в той же транзакции, что и показатели
        _audit.Add(User, AuditActions.AnalysisConfirmed, id, document.UserId, $"{request.Indicators.Count} indicators");
        var saved = await _analysis.ConfirmAsync(document, document.User!, request, User.GetUserId()!.Value, cancellationToken);
        if (saved == null)
        {
            return Problem(detail: "This document has not been analyzed yet", statusCode: StatusCodes.Status404NotFound);
        }

        var updated = await _context.MedicalDocuments
            .AsNoTracking()
            .Include(d => d.TestResults)
            .FirstAsync(d => d.Id == id, cancellationToken);
        return updated.ToDetailsResponse();
    }

    // Документ с владельцем, если он есть и текущий пользователь имеет к нему доступ; иначе — ответ с ошибкой
    private async Task<(MedicalDocument? Document, ActionResult? Error)> FindAccessibleDocumentAsync(int id, CancellationToken cancellationToken)
    {
        var document = await _context.MedicalDocuments
            .AsNoTracking()
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (document?.User == null)
        {
            return (null, DocumentNotFound());
        }

        return User.CanAccessDocument(document) ? (document, null) : (null, Forbid());
    }

    /// <summary>Change a document's processing status (Doctor, Admin)</summary>
    /// <remarks>
    /// Status values: <c>Pending</c>, <c>Processing</c>, <c>Completed</c>, <c>Error</c>.
    /// A document also becomes <c>Completed</c> automatically when its first test result is added.
    /// </remarks>
    /// <response code="200">The updated document.</response>
    /// <response code="400">Unknown status value.</response>
    /// <response code="404">Document not found.</response>
    [HttpPut("{id:int}/status")]
    [Authorize(Roles = Roles.Staff)]
    [ProducesResponseType<DocumentDetailsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DocumentDetailsResponse>> UpdateStatus(int id, UpdateDocumentStatusRequest request)
    {
        var document = await _context.MedicalDocuments
            .Include(d => d.TestResults)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (document == null)
        {
            return DocumentNotFound();
        }

        document.Status = request.Status;
        await _context.SaveChangesAsync();

        return document.ToDetailsResponse();
    }

    /// <summary>Delete a document (owner or Admin)</summary>
    /// <remarks>Also deletes its test results and the stored file.</remarks>
    /// <response code="204">Document deleted.</response>
    /// <response code="403">Not the owner and not Admin.</response>
    /// <response code="404">Document not found.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteDocument(int id)
    {
        var document = await _context.MedicalDocuments.FindAsync(id);

        if (document == null)
        {
            return DocumentNotFound();
        }

        if (document.UserId != User.GetUserId() && !User.IsInRole(Roles.Admin))
        {
            return Forbid();
        }

        // Показатели удаляются каскадно (ON DELETE CASCADE в БД); запись журнала остаётся — по ней видно, кто удалил
        _context.MedicalDocuments.Remove(document);
        _audit.Add(User, AuditActions.DocumentDeleted, document.Id, document.UserId, document.OriginalFileName);
        await _context.SaveChangesAsync();
        _storage.Delete(document.FilePath);

        return NoContent();
    }

    /// <summary>History of a document</summary>
    /// <remarks>
    /// Who uploaded the document (and from which laboratory), who ran the AI analysis and who confirmed the values — oldest first.
    /// Allowed for everyone who can open the document.
    /// </remarks>
    /// <response code="200">The events.</response>
    /// <response code="403">No access to the document.</response>
    /// <response code="404">Document not found.</response>
    [HttpGet("{id:int}/history")]
    [ProducesResponseType<List<AuditEventResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<AuditEventResponse>>> GetHistory(int id, CancellationToken cancellationToken)
    {
        var (document, error) = await FindAccessibleDocumentAsync(id, cancellationToken);
        if (document == null)
        {
            return error!;
        }

        return await _context.AuditEvents
            .AsNoTracking()
            .Where(e => e.DocumentId == id)
            .OrderBy(e => e.At)
            .Select(e => new AuditEventResponse(
                e.At,
                e.UserName,
                e.UserRole,
                _context.Laboratories.Where(l => l.Id == e.LaboratoryId).Select(l => l.Name).FirstOrDefault(),
                e.Action,
                e.Details))
            .ToListAsync(cancellationToken);
    }

    private ObjectResult DocumentNotFound() => Problem(detail: "Document not found", statusCode: StatusCodes.Status404NotFound);
}
