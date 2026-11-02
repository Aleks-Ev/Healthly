using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using HealthlyApi.Data;
using HealthlyApi.Dtos;
using HealthlyApi.Models;
using HealthlyApi.Services;

namespace HealthlyApi.Controllers;

// Рабочее место сотрудника лаборатории (роль LabStaff):
//   1. пациент называет Healthly ID или показывает QR-код (сканер QR в лаборатории «печатает» код как клавиатура);
//   2. GET  /api/lab/patients/{code}            — найти пациента: имя, дата рождения, пол (сверить с документом);
//   3. POST /api/lab/patients/{code}/documents  — загрузить бланк с компьютера лаборатории;
//   4. GET  /api/lab/documents                  — очередь лаборатории: не проанализированы / черновик / подтверждены;
//   5. дальше — обычный экран документа: ИИ-анализ, сверка, «Confirm & save» (доступ — CanAccessDocument).
// Сотрудник видит только документы своей лаборатории; каждое действие пишется в журнал (AuditLog).
[ApiController]
[Route("api/lab")]
[Tags("Laboratory Workspace")]
[Authorize(Roles = Roles.LabStaff)]
[Produces("application/json")]
public class LabController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly FileStorageService _storage;
    private readonly AuditLog _audit;

    public LabController(AppDbContext context, FileStorageService storage, AuditLog audit)
    {
        _context = context;
        _storage = storage;
        _audit = audit;
    }

    /// <summary>Find a patient by Healthly ID (Lab staff)</summary>
    /// <remarks>
    /// The patient tells their Healthly ID (e.g. <c>HL-7K3P9Q</c>) or shows its QR code; spaces, dashes and lower case are accepted.
    /// Returns only what is needed to make sure it is the right person: name, date of birth, gender.
    /// </remarks>
    /// <response code="200">The patient.</response>
    /// <response code="403">The account is not linked to a laboratory.</response>
    /// <response code="404">No patient with this Healthly ID.</response>
    /// <response code="429">More than 30 lookups in a minute; wait for the time in <c>Retry-After</c> (seconds).</response>
    [HttpGet("patients/{code}")]
    [EnableRateLimiting(RateLimiting.PatientLookup)]
    [ProducesResponseType<LabPatientResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<LabPatientResponse>> FindPatient(string code, CancellationToken cancellationToken)
    {
        if (User.GetLaboratoryId() == null)
        {
            return NoLaboratory();
        }

        var patient = await FindPatientAsync(code, cancellationToken);
        if (patient == null)
        {
            return PatientNotFound();
        }

        _audit.Add(User, AuditActions.PatientLookedUp, patientId: patient.Id, details: patient.PatientCode!);
        await _context.SaveChangesAsync(cancellationToken);

        return new LabPatientResponse(patient.Id, patient.PatientCode!, patient.FullName, patient.DateOfBirth, patient.Gender);
    }

    /// <summary>Upload a patient's lab report (Lab staff)</summary>
    /// <remarks>
    /// <c>multipart/form-data</c> with a single field <c>file</c> (PDF, PNG or JPG, up to 10 MB) — a file from the laboratory computer.
    /// The document belongs to the patient and is linked to the staff member's laboratory; then analyze it with
    /// <c>POST /api/documents/{id}/analyze</c> and confirm with <c>POST /api/documents/{id}/analysis/confirm</c>.
    /// </remarks>
    /// <response code="201">Document uploaded.</response>
    /// <response code="400">No file, a wrong file type or a file over 10 MB.</response>
    /// <response code="403">The account is not linked to a laboratory.</response>
    /// <response code="404">No patient with this Healthly ID.</response>
    [HttpPost("patients/{code}/documents")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(FileStorageService.MaxFileSizeBytes + 64 * 1024)]
    [ProducesResponseType<DocumentDetailsResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DocumentDetailsResponse>> Upload(string code, IFormFile file, CancellationToken cancellationToken)
    {
        if (User.GetLaboratoryId() is not { } laboratoryId)
        {
            return NoLaboratory();
        }

        var patient = await FindPatientAsync(code, cancellationToken);
        if (patient == null)
        {
            return PatientNotFound();
        }

        if (FileStorageService.ValidateUpload(file) is { } invalid)
        {
            return Problem(detail: invalid, statusCode: StatusCodes.Status400BadRequest);
        }

        var document = new MedicalDocument
        {
            UserId = patient.Id,
            UploadedByUserId = User.GetUserId(),
            LaboratoryId = laboratoryId,
            OriginalFileName = Path.GetFileName(file.FileName),
            FilePath = await _storage.SaveAsync(file, cancellationToken),
            ContentType = FileStorageService.GetContentType(file.FileName)!,
            FileSizeBytes = file.Length,
        };
        _context.MedicalDocuments.Add(document);
        await _context.SaveChangesAsync(cancellationToken);

        _audit.Add(User, AuditActions.DocumentUploaded, document.Id, patient.Id, document.OriginalFileName);
        await _context.SaveChangesAsync(cancellationToken);

        return Created($"/api/documents/{document.Id}", document.ToDetailsResponse());
    }

    /// <summary>The laboratory's documents (Lab staff)</summary>
    /// <remarks>
    /// Documents uploaded by the staff member's laboratory, newest first. <c>analysisStatus</c>: <c>NotAnalyzed</c>,
    /// <c>Draft</c> (analyzed, waiting for confirmation) or <c>Confirmed</c>. <c>?pending=true</c> — only the ones that are not confirmed yet.
    /// </remarks>
    /// <response code="200">The documents.</response>
    /// <response code="403">The account is not linked to a laboratory.</response>
    [HttpGet("documents")]
    [ProducesResponseType<List<LabDocumentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<List<LabDocumentResponse>>> GetDocuments(CancellationToken cancellationToken, [FromQuery] bool pending = false)
    {
        if (User.GetLaboratoryId() is not { } laboratoryId)
        {
            return NoLaboratory();
        }

        var rows = await _context.MedicalDocuments
            .AsNoTracking()
            .Where(d => d.LaboratoryId == laboratoryId)
            .OrderByDescending(d => d.UploadDate)
            .Take(200)
            .Select(d => new
            {
                d.Id,
                d.UserId,
                PatientName = d.User!.FullName,
                PatientCode = d.User.PatientCode ?? string.Empty,
                d.OriginalFileName,
                d.UploadDate,
                UploadedBy = _context.Users.Where(u => u.Id == d.UploadedByUserId).Select(u => u.FullName).FirstOrDefault() ?? string.Empty,
                Analysis = _context.DocumentAnalyses.Where(a => a.MedicalDocumentId == d.Id).Select(a => new { a.ConfirmedAt }).FirstOrDefault(),
                ResultsCount = d.TestResults.Count,
                CriticalCount = d.TestResults.Count(r => r.IsCritical),
            })
            .ToListAsync(cancellationToken);

        var documents = rows
            .Select(r => new LabDocumentResponse(
                r.Id,
                r.UserId,
                r.PatientName,
                r.PatientCode,
                r.OriginalFileName,
                r.UploadDate,
                r.UploadedBy,
                r.Analysis == null ? LabAnalysisStatuses.NotAnalyzed
                    : r.Analysis.ConfirmedAt == null ? LabAnalysisStatuses.Draft
                    : LabAnalysisStatuses.Confirmed,
                r.ResultsCount,
                r.CriticalCount))
            .Where(d => !pending || d.AnalysisStatus != LabAnalysisStatuses.Confirmed)
            .ToList();
        return documents;
    }

    private Task<User?> FindPatientAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = PatientCodes.Normalize(code);
        return _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.PatientCode == normalized && u.Role == Roles.Patient, cancellationToken);
    }

    private ObjectResult NoLaboratory() =>
        Problem(detail: "Your account is not linked to a laboratory. Ask an administrator to assign one.", statusCode: StatusCodes.Status403Forbidden);

    private ObjectResult PatientNotFound() =>
        Problem(detail: "No patient with this Healthly ID. Check the code and try again.", statusCode: StatusCodes.Status404NotFound);
}
