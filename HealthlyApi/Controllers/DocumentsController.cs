using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public DocumentsController(AppDbContext context, FileStorageService storage, DocumentAnalysisService analysis)
    {
        _context = context;
        _storage = storage;
        _analysis = analysis;
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

        var contentType = FileStorageService.GetContentType(file.FileName);

        if (contentType == null)
        {
            return Problem(detail: $"Allowed file types: {string.Join(", ", FileStorageService.AllowedExtensions)}", statusCode: StatusCodes.Status400BadRequest);
        }

        if (file.Length == 0 || file.Length > FileStorageService.MaxFileSizeBytes)
        {
            return Problem(detail: "File must be between 1 byte and 10 MB", statusCode: StatusCodes.Status400BadRequest);
        }

        var document = new MedicalDocument
        {
            UserId = userId,
            OriginalFileName = Path.GetFileName(file.FileName),
            FilePath = await _storage.SaveAsync(file, cancellationToken),
            ContentType = contentType,
            FileSizeBytes = file.Length,
        };

        _context.MedicalDocuments.Add(document);
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

        if (!User.CanAccessUser(document.UserId))
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

        if (!User.CanAccessUser(document.UserId))
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
    /// Each indicator is compared with the reference norms (<c>isAbnormal</c>, <c>category</c>, <c>doctorSpecialty</c>);
    /// <c>suggestedSpecialists</c> lists the doctors for the out-of-range ones.
    ///
    /// <b>Currently a mock</b> (<c>isMock: true</c>, <c>model: "mock-ai-v1"</c>): the values are generated, not read from the file,
    /// and stay the same for the same document. The response format will not change when real OCR and LLM are connected.
    /// Nothing is saved: results are added to the document with <c>POST /api/documents/{id}/results</c>.
    /// Allowed for the owner, Doctor and Admin.
    /// </remarks>
    /// <response code="200">The analysis.</response>
    /// <response code="403">A patient tried to analyze another user's document.</response>
    /// <response code="404">Document not found, or its file is missing from storage.</response>
    [HttpPost("{id:int}/analyze")]
    [ProducesResponseType<DocumentAnalysisResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentAnalysisResponse>> Analyze(int id, CancellationToken cancellationToken)
    {
        var document = await _context.MedicalDocuments
            .AsNoTracking()
            .Include(d => d.User)
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (document?.User == null)
        {
            return DocumentNotFound();
        }

        if (!User.CanAccessUser(document.UserId))
        {
            return Forbid();
        }

        var result = await _analysis.AnalyzeAsync(document, document.User, cancellationToken);

        if (result == null)
        {
            return Problem(detail: "File is missing from storage", statusCode: StatusCodes.Status404NotFound);
        }

        return result;
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

        // Показатели удаляются каскадно (ON DELETE CASCADE в БД)
        _context.MedicalDocuments.Remove(document);
        await _context.SaveChangesAsync();
        _storage.Delete(document.FilePath);

        return NoContent();
    }

    private ObjectResult DocumentNotFound() => Problem(detail: "Document not found", statusCode: StatusCodes.Status404NotFound);
}
