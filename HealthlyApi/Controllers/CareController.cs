using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HealthlyApi.Data;
using HealthlyApi.Dtos;
using HealthlyApi.Models;
using HealthlyApi.Services;
using HealthlyApi.Services.Ai;
using HealthlyApi.Services.Care;

namespace HealthlyApi.Controllers;

// «Моя команда здоровья»: пациент выбирает врача и лабораторию в своём городе.
//   1. GET  /api/care/suggestions          — врачи и лаборатории города, сначала менее загруженные;
//   2. POST /api/care/connections          — пациент отправляет запрос (Requested), врачу / сотрудникам лаборатории — уведомление;
//   3. POST /api/care/connections/{id}/accept | decline — врач или сотрудник лаборатории отвечает;
//   4. пациенту приходит уведомление «поделиться данными?» → POST /{id}/confirm (Active) или /{id}/end (отказ);
//   5. POST /{id}/end — любая сторона завершает связь, доступ врача пропадает сразу.
// Active-связь с врачом открывает ему медкарту (CareAccess). Лаборатория данных пациента не видит —
// пациент появляется в её списке с Healthly ID, чтобы сотрудник загрузил новый бланк (POST /api/lab/patients/{code}/documents).
// Каждое изменение пишется в журнал (AuditLog) и создаёт уведомление второй стороне (Notifier) — одним SaveChanges.
[ApiController]
[Route("api/care")]
[Tags("Care team")]
[Authorize]
[Produces("application/json")]
public class CareController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly AuditLog _audit;
    private readonly Notifier _notifier;

    public CareController(AppDbContext context, AuditLog audit, Notifier notifier)
    {
        _context = context;
        _audit = audit;
        _notifier = notifier;
    }

    /// <summary>Doctors and laboratories to choose from (Patient)</summary>
    /// <remarks>
    /// Providers in the patient's city (or <c>?city=</c>) that accept new patients, without those the patient already
    /// has an open request or connection with. Sorted by <c>patientCount</c> ascending — doctors and laboratories with
    /// fewer patients come first — then by name. <c>city</c>: one of the testing cities; defaults to the profile city.
    /// </remarks>
    /// <response code="200">The suggestions. <c>city</c> is empty if the profile has no city and none was given.</response>
    /// <response code="400">Unknown city.</response>
    [HttpGet("suggestions")]
    [Authorize(Roles = Roles.Patient)]
    [ProducesResponseType<CareSuggestionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CareSuggestionsResponse>> GetSuggestions([FromQuery] string? city, CancellationToken cancellationToken)
    {
        var patientId = User.GetUserId();
        var patient = await _context.Users.AsNoTracking().FirstAsync(u => u.Id == patientId, cancellationToken);

        var selectedCity = string.IsNullOrWhiteSpace(city) ? patient.City : city.Trim();
        if (string.IsNullOrEmpty(selectedCity))
        {
            return new CareSuggestionsResponse(string.Empty, [], []);
        }
        if (!TestingCities.All.Contains(selectedCity))
        {
            return Problem(detail: "Select one of the testing cities", statusCode: StatusCodes.Status400BadRequest);
        }

        // Врачи и лаборатории, с которыми у пациента уже есть открытая связь, не предлагаются повторно
        var openConnections = _context.CareConnections
            .Where(c => c.PatientId == patientId && CareConnectionStatuses.Open.Contains(c.Status));
        var loadStatuses = new[] { CareConnectionStatuses.Active, CareConnectionStatuses.Accepted };

        var doctors = await _context.Users
            .AsNoTracking()
            .Where(u => u.Role == Roles.Doctor && u.City == selectedCity && u.AcceptingPatients)
            .Where(u => !openConnections.Any(c => c.DoctorId == u.Id))
            .Select(u => new DoctorSuggestion(
                u.Id, u.FullName, u.Specialty, u.Workplace, u.City, u.Bio, u.Languages,
                _context.CareConnections.Count(c => c.DoctorId == u.Id && loadStatuses.Contains(c.Status))))
            .ToListAsync(cancellationToken);

        var laboratories = await _context.Laboratories
            .AsNoTracking()
            .Where(l => l.City == selectedCity && l.AcceptingPatients)
            .Where(l => !openConnections.Any(c => c.LaboratoryId == l.Id))
            .Select(l => new LaboratorySuggestion(
                l.Id, l.Name, l.City, l.Address, l.Description, l.OpeningHours, l.Phone,
                _context.CareConnections.Count(c => c.LaboratoryId == l.Id && loadStatuses.Contains(c.Status))))
            .ToListAsync(cancellationToken);

        return new CareSuggestionsResponse(
            selectedCity,
            CareRules.RankByLoad(doctors, d => d.PatientCount, d => d.FullName).ToList(),
            CareRules.RankByLoad(laboratories, l => l.PatientCount, l => l.Name).ToList());
    }

    /// <summary>Care connections of the current user</summary>
    /// <remarks>
    /// Patient — their requests and connections; Doctor — requests to them and their patients;
    /// Lab staff — those of their laboratory; Admin — all (optionally <c>?patientId=</c>). Newest first.
    /// A provider sees the patient's Healthly ID only while the connection is <c>Active</c>.
    /// </remarks>
    /// <response code="200">The connections.</response>
    [HttpGet("connections")]
    [ProducesResponseType<List<CareConnectionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<List<CareConnectionResponse>>> GetConnections([FromQuery] int? patientId, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var query = WithParties(_context.CareConnections.AsNoTracking());

        if (User.IsInRole(Roles.Patient))
        {
            query = query.Where(c => c.PatientId == userId);
        }
        else if (User.IsInRole(Roles.Doctor))
        {
            query = query.Where(c => c.DoctorId == userId);
        }
        else if (User.IsInRole(Roles.LabStaff))
        {
            var laboratoryId = User.GetLaboratoryId();
            query = query.Where(c => laboratoryId != null && c.LaboratoryId == laboratoryId);
        }
        else if (patientId != null)
        {
            query = query.Where(c => c.PatientId == patientId);
        }

        var connections = await query.OrderByDescending(c => c.RequestedAt).Take(500).ToListAsync(cancellationToken);
        return connections.Select(ToResponse).ToList();
    }

    /// <summary>Request a doctor or a laboratory (Patient)</summary>
    /// <remarks>
    /// Send exactly one of <c>doctorId</c> or <c>laboratoryId</c> and an optional <c>message</c> (up to 300 characters).
    /// The doctor (or every staff member of the laboratory) gets a notification. Nothing is shared yet:
    /// after the provider accepts, the patient confirms sharing with <c>POST /api/care/connections/{id}/confirm</c>.
    /// At most 5 requests can wait for an answer at the same time.
    /// </remarks>
    /// <response code="201">Request sent (status <c>Requested</c>).</response>
    /// <response code="400">Validation failed, or the provider does not accept new patients.</response>
    /// <response code="404">Doctor or laboratory not found.</response>
    /// <response code="409">There is already an open request or connection with this provider, or 5 requests are waiting.</response>
    [HttpPost("connections")]
    [Authorize(Roles = Roles.Patient)]
    [ProducesResponseType<CareConnectionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CareConnectionResponse>> RequestConnection(CreateCareConnectionRequest request, CancellationToken cancellationToken)
    {
        if ((request.DoctorId == null) == (request.LaboratoryId == null))
        {
            ModelState.AddModelError(nameof(request.DoctorId), "Choose either a doctor or a laboratory");
            return ValidationProblem(ModelState);
        }

        var patientId = User.GetUserId()!.Value;
        var patient = await _context.Users.FirstAsync(u => u.Id == patientId, cancellationToken);

        var pending = await _context.CareConnections.CountAsync(
            c => c.PatientId == patientId && c.Status == CareConnectionStatuses.Requested, cancellationToken);
        if (pending >= CareRules.MaxPendingRequests)
        {
            return Problem(
                detail: $"You already have {CareRules.MaxPendingRequests} requests waiting for an answer. Wait for a reply or withdraw one of them.",
                statusCode: StatusCodes.Status409Conflict);
        }

        var connection = new CareConnection { PatientId = patientId, PatientMessage = request.Message?.Trim() ?? string.Empty };

        if (request.DoctorId is { } doctorId)
        {
            var doctor = await _context.Users.FirstOrDefaultAsync(u => u.Id == doctorId && u.Role == Roles.Doctor, cancellationToken);
            if (doctor == null)
            {
                return Problem(detail: "Doctor not found", statusCode: StatusCodes.Status404NotFound);
            }
            if (!doctor.AcceptingPatients)
            {
                return Problem(detail: "This doctor is not accepting new patients", statusCode: StatusCodes.Status400BadRequest);
            }
            if (await HasOpenConnectionAsync(patientId, doctorId, null, cancellationToken))
            {
                return AlreadyConnected();
            }

            connection.ProviderType = CareProviderTypes.Doctor;
            connection.DoctorId = doctorId;
            connection.Doctor = doctor;
            _notifier.ToUser(doctorId, NotificationTypes.CareRequestReceived, patient.FullName,
                "New patient request",
                $"{patient.FullName} would like you to be their doctor on Healthly.",
                Notifier.CareLink, connection);
        }
        else
        {
            var laboratoryId = request.LaboratoryId!.Value;
            var laboratory = await _context.Laboratories.FirstOrDefaultAsync(l => l.Id == laboratoryId, cancellationToken);
            if (laboratory == null)
            {
                return Problem(detail: "Laboratory not found", statusCode: StatusCodes.Status404NotFound);
            }
            if (!laboratory.AcceptingPatients)
            {
                return Problem(detail: "This laboratory is not accepting new patients", statusCode: StatusCodes.Status400BadRequest);
            }
            if (await HasOpenConnectionAsync(patientId, null, laboratoryId, cancellationToken))
            {
                return AlreadyConnected();
            }

            connection.ProviderType = CareProviderTypes.Laboratory;
            connection.LaboratoryId = laboratoryId;
            connection.Laboratory = laboratory;
            await _notifier.ToLaboratoryStaffAsync(laboratoryId, NotificationTypes.CareRequestReceived, patient.FullName,
                "New patient request",
                $"{patient.FullName} chose {laboratory.Name} as their laboratory.",
                connection, cancellationToken);
        }

        connection.Patient = patient;
        _context.CareConnections.Add(connection);
        _audit.Add(User, AuditActions.CareRequested, patientId: patientId, details: ProviderName(connection));

        if (!await TrySaveAsync(cancellationToken))
        {
            return AlreadyConnected();
        }

        return Created($"/api/care/connections/{connection.Id}", ToResponse(connection));
    }

    /// <summary>Accept a patient's request (Doctor, Lab staff)</summary>
    /// <remarks>The patient gets a notification asking whether to share their records (doctor) or let the laboratory add results.</remarks>
    /// <response code="200">Status <c>Accepted</c>; waiting for the patient.</response>
    /// <response code="403">The request is addressed to another doctor or laboratory.</response>
    /// <response code="404">Connection not found.</response>
    /// <response code="409">The request was already answered or withdrawn.</response>
    [HttpPost("connections/{id:int}/accept")]
    [Authorize(Roles = Roles.Doctor + "," + Roles.LabStaff)]
    [ProducesResponseType<CareConnectionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CareConnectionResponse>> Accept(int id, CancellationToken cancellationToken)
    {
        var (connection, error) = await FindForProviderAsync(id, cancellationToken);
        if (connection == null)
        {
            return error!;
        }
        if (Transition(connection, CareAction.Accept, CareActor.Provider) is { } invalid)
        {
            return invalid;
        }

        connection.RespondedAt = DateTime.UtcNow;
        connection.RespondedByUserId = User.GetUserId();

        var provider = ProviderName(connection);
        var isDoctor = connection.ProviderType == CareProviderTypes.Doctor;
        _notifier.ToUser(connection.PatientId, NotificationTypes.CareRequestAccepted, provider,
            $"{provider} accepted your request",
            isDoctor
                ? $"Share your medical records with {provider}? Your doctor can see them only after you confirm."
                : $"Allow {provider} to add new lab results to your account? The laboratory does not see your medical records.",
            Notifier.CareLink, connection);
        _audit.Add(User, AuditActions.CareAccepted, patientId: connection.PatientId, details: provider);
        await _context.SaveChangesAsync(cancellationToken);

        return ToResponse(connection);
    }

    /// <summary>Decline a patient's request (Doctor, Lab staff)</summary>
    /// <remarks>Optional <c>note</c> for the patient (up to 300 characters), e.g. "I only see patients with diabetes".</remarks>
    /// <response code="200">Status <c>Declined</c>.</response>
    /// <response code="403">The request is addressed to another doctor or laboratory.</response>
    /// <response code="404">Connection not found.</response>
    /// <response code="409">The request was already answered or withdrawn.</response>
    [HttpPost("connections/{id:int}/decline")]
    [Authorize(Roles = Roles.Doctor + "," + Roles.LabStaff)]
    [ProducesResponseType<CareConnectionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CareConnectionResponse>> Decline(int id, CareDecisionRequest? request, CancellationToken cancellationToken)
    {
        var (connection, error) = await FindForProviderAsync(id, cancellationToken);
        if (connection == null)
        {
            return error!;
        }
        if (Transition(connection, CareAction.Decline, CareActor.Provider) is { } invalid)
        {
            return invalid;
        }

        connection.RespondedAt = DateTime.UtcNow;
        connection.RespondedByUserId = User.GetUserId();
        connection.ProviderNote = request?.Note?.Trim() ?? string.Empty;

        var provider = ProviderName(connection);
        var kind = connection.ProviderType == CareProviderTypes.Doctor ? "doctor" : "laboratory";
        _notifier.ToUser(connection.PatientId, NotificationTypes.CareRequestDeclined, provider,
            $"{provider} declined your request",
            connection.ProviderNote.Length > 0
                ? $"{connection.ProviderNote} You can choose another {kind} in your city."
                : $"You can choose another {kind} in your city.",
            Notifier.CareLink, connection);
        _audit.Add(User, AuditActions.CareDeclined, patientId: connection.PatientId, details: provider);
        await _context.SaveChangesAsync(cancellationToken);

        return ToResponse(connection);
    }

    /// <summary>Share records with an accepted doctor / allow an accepted laboratory (Patient)</summary>
    /// <remarks>
    /// For a doctor: from now on the doctor can open the patient's records (documents, results, trends) until the
    /// connection ends. For a laboratory: the patient appears in the laboratory's list with their Healthly ID so its staff
    /// can upload new lab reports; the laboratory still cannot see the patient's records.
    /// </remarks>
    /// <response code="200">Status <c>Active</c>.</response>
    /// <response code="403">Not your connection.</response>
    /// <response code="404">Connection not found.</response>
    /// <response code="409">The provider has not accepted yet, or the connection was already confirmed or ended.</response>
    [HttpPost("connections/{id:int}/confirm")]
    [Authorize(Roles = Roles.Patient)]
    [ProducesResponseType<CareConnectionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CareConnectionResponse>> Confirm(int id, CancellationToken cancellationToken)
    {
        var (connection, error) = await FindForPatientAsync(id, cancellationToken);
        if (connection == null)
        {
            return error!;
        }
        if (Transition(connection, CareAction.Confirm, CareActor.Patient) is { } invalid)
        {
            return invalid;
        }

        connection.ConfirmedAt = DateTime.UtcNow;

        var patientName = connection.Patient!.FullName;
        if (connection.DoctorId is { } doctorId)
        {
            _notifier.ToUser(doctorId, NotificationTypes.CareSharingConfirmed, patientName,
                $"{patientName} shared their records with you",
                "You can now see their documents, results and trends.",
                $"/admin/users/{connection.PatientId}", connection);
        }
        else
        {
            await _notifier.ToLaboratoryStaffAsync(connection.LaboratoryId!.Value, NotificationTypes.CareSharingConfirmed, patientName,
                $"{patientName} confirmed {connection.Laboratory!.Name}",
                "You can upload their lab reports; their Healthly ID is in your patient list.",
                connection, cancellationToken);
        }
        _audit.Add(User, AuditActions.CareSharingConfirmed, patientId: connection.PatientId, details: ProviderName(connection));
        await _context.SaveChangesAsync(cancellationToken);

        return ToResponse(connection);
    }

    /// <summary>Withdraw, refuse or end a connection (Patient, Doctor, Lab staff)</summary>
    /// <remarks>
    /// Patient: withdraws a waiting request, refuses to share after the provider accepted (<c>Cancelled</c>),
    /// or stops sharing an active connection (<c>Ended</c>) — the doctor loses access immediately.
    /// Doctor / lab staff: end an accepted or active connection (<c>Ended</c>). The other side gets a notification.
    /// </remarks>
    /// <response code="200">Status <c>Cancelled</c> or <c>Ended</c>.</response>
    /// <response code="403">Not your connection.</response>
    /// <response code="404">Connection not found.</response>
    /// <response code="409">The connection is already closed.</response>
    [HttpPost("connections/{id:int}/end")]
    [Authorize(Roles = Roles.Patient + "," + Roles.Doctor + "," + Roles.LabStaff)]
    [ProducesResponseType<CareConnectionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CareConnectionResponse>> End(int id, CareDecisionRequest? request, CancellationToken cancellationToken)
    {
        var isPatient = User.IsInRole(Roles.Patient);
        var (connection, error) = isPatient
            ? await FindForPatientAsync(id, cancellationToken)
            : await FindForProviderAsync(id, cancellationToken);
        if (connection == null)
        {
            return error!;
        }

        var wasActive = connection.Status == CareConnectionStatuses.Active;
        if (Transition(connection, CareAction.End, isPatient ? CareActor.Patient : CareActor.Provider) is { } invalid)
        {
            return invalid;
        }

        connection.EndedAt = DateTime.UtcNow;
        connection.EndedByUserId = User.GetUserId();
        var note = request?.Note?.Trim() ?? string.Empty;

        var provider = ProviderName(connection);
        var patientName = connection.Patient!.FullName;
        if (isPatient)
        {
            // Врачу/лаборатории сообщаем, только если запрос уже дошёл до них и был принят или активен
            var title = wasActive ? $"{patientName} stopped sharing with you" : $"{patientName} withdrew their request";
            var body = wasActive
                ? (connection.DoctorId != null ? "You no longer have access to their records." : "They are no longer in your patient list.")
                : "No action is needed.";
            if (connection.DoctorId is { } doctorId)
            {
                _notifier.ToUser(doctorId, NotificationTypes.CareConnectionEnded, patientName, title, body, Notifier.CareLink, connection);
            }
            else
            {
                await _notifier.ToLaboratoryStaffAsync(connection.LaboratoryId!.Value, NotificationTypes.CareConnectionEnded, patientName,
                    title, body, connection, cancellationToken);
            }
        }
        else
        {
            connection.ProviderNote = note;
            _notifier.ToUser(connection.PatientId, NotificationTypes.CareConnectionEnded, provider,
                $"{provider} ended the connection",
                note.Length > 0 ? note : "You can choose another provider in your city at any time.",
                Notifier.CareLink, connection);
        }
        _audit.Add(User, AuditActions.CareEnded, patientId: connection.PatientId, details: $"{provider}: {connection.Status}");
        await _context.SaveChangesAsync(cancellationToken);

        return ToResponse(connection);
    }

    // --- Вспомогательные ---

    private static IQueryable<CareConnection> WithParties(IQueryable<CareConnection> query) =>
        query.Include(c => c.Patient).Include(c => c.Doctor).Include(c => c.Laboratory);

    // Связь, адресованная этому врачу / лаборатории этого сотрудника
    private async Task<(CareConnection? Connection, ActionResult? Error)> FindForProviderAsync(int id, CancellationToken cancellationToken)
    {
        var connection = await WithParties(_context.CareConnections).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (connection == null)
        {
            return (null, ConnectionNotFound());
        }

        var isMine = User.IsInRole(Roles.Doctor)
            ? connection.DoctorId != null && connection.DoctorId == User.GetUserId()
            : connection.LaboratoryId != null && connection.LaboratoryId == User.GetLaboratoryId();
        return isMine ? (connection, null) : (null, Forbid());
    }

    private async Task<(CareConnection? Connection, ActionResult? Error)> FindForPatientAsync(int id, CancellationToken cancellationToken)
    {
        var connection = await WithParties(_context.CareConnections).FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (connection == null)
        {
            return (null, ConnectionNotFound());
        }
        return connection.PatientId == User.GetUserId() ? (connection, null) : (null, Forbid());
    }

    // Меняет статус по правилам CareRules; null — успех, иначе 409
    private ObjectResult? Transition(CareConnection connection, CareAction action, CareActor actor)
    {
        var next = CareRules.NextStatus(connection.Status, action, actor);
        if (next == null)
        {
            return Problem(detail: $"This action is not possible while the connection is {connection.Status}", statusCode: StatusCodes.Status409Conflict);
        }
        connection.Status = next;
        return null;
    }

    private Task<bool> HasOpenConnectionAsync(int patientId, int? doctorId, int? laboratoryId, CancellationToken cancellationToken) =>
        _context.CareConnections.AnyAsync(c =>
            c.PatientId == patientId
            && CareConnectionStatuses.Open.Contains(c.Status)
            && (doctorId != null ? c.DoctorId == doctorId : c.LaboratoryId == laboratoryId),
            cancellationToken);

    // Два одновременных запроса к одному врачу: второй упрётся в частичный уникальный индекс → 409, а не 500
    private async Task<bool> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation })
        {
            return false;
        }
    }

    private static string ProviderName(CareConnection connection) =>
        connection.Doctor?.FullName ?? connection.Laboratory?.Name ?? string.Empty;

    private static CareConnectionResponse ToResponse(CareConnection connection)
    {
        var patient = connection.Patient!;
        var provider = connection.Doctor is { } doctor
            ? new CareProviderResponse(
                doctor.Id,
                doctor.FullName,
                string.Join(" · ", new[] { doctor.Specialty, doctor.Workplace }.Where(s => !string.IsNullOrWhiteSpace(s))),
                doctor.City,
                doctor.Bio)
            : new CareProviderResponse(
                connection.Laboratory!.Id,
                connection.Laboratory.Name,
                connection.Laboratory.Address,
                connection.Laboratory.City,
                connection.Laboratory.Description);

        return new CareConnectionResponse(
            connection.Id,
            connection.ProviderType,
            connection.Status,
            new CarePatientResponse(
                patient.Id,
                patient.FullName,
                patient.City,
                patient.Gender,
                DocumentAnalysisService.GetAge(patient.DateOfBirth),
                // Healthly ID — только пока связь активна (пациент свой код и так видит в профиле)
                connection.Status == CareConnectionStatuses.Active ? patient.PatientCode : null),
            provider,
            connection.PatientMessage,
            connection.ProviderNote,
            connection.RequestedAt,
            connection.RespondedAt,
            connection.ConfirmedAt,
            connection.EndedAt);
    }

    private ObjectResult ConnectionNotFound() =>
        Problem(detail: "Connection not found", statusCode: StatusCodes.Status404NotFound);

    private ObjectResult AlreadyConnected() =>
        Problem(detail: "You already have an open request or connection with this provider", statusCode: StatusCodes.Status409Conflict);
}
