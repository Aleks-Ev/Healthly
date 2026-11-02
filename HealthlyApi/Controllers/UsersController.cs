using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HealthlyApi.Data;
using HealthlyApi.Dtos;
using HealthlyApi.Models;
using HealthlyApi.Services;

namespace HealthlyApi.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PasswordService _passwords;
    private readonly FileStorageService _storage;
    private readonly RefreshTokenService _refreshTokens;

    // Внедряем зависимости через конструктор (Dependency Injection)
    public UsersController(AppDbContext context, PasswordService passwords, FileStorageService storage, RefreshTokenService refreshTokens)
    {
        _context = context;
        _passwords = passwords;
        _storage = storage;
        _refreshTokens = refreshTokens;
    }

    /// <summary>List users (Doctor, Admin)</summary>
    /// <remarks>Sorted by id (oldest accounts first). Optional filter: <c>?role=Patient</c>, <c>Doctor</c> or <c>Admin</c>.</remarks>
    /// <param name="role">Optional role filter.</param>
    /// <response code="200">The list of users.</response>
    [HttpGet]
    [Authorize(Roles = Roles.Staff)]
    [ProducesResponseType<IEnumerable<UserResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetUsers([FromQuery] string? role)
    {
        IQueryable<User> query = _context.Users.AsNoTracking().Include(u => u.Laboratory);

        if (!string.IsNullOrWhiteSpace(role))
        {
            query = query.Where(u => u.Role == role);
        }

        var users = await query.OrderBy(u => u.Id).ToListAsync();
        return users.Select(u => u.ToResponse()).ToList();
    }

    /// <summary>Get a user by id</summary>
    /// <remarks>Allowed for the user themselves, Doctor and Admin.</remarks>
    /// <response code="200">The user.</response>
    /// <response code="403">A patient requested another user's profile.</response>
    /// <response code="404">User not found.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetUser(int id)
    {
        if (!User.CanAccessUser(id))
        {
            return Forbid();
        }

        var user = await _context.Users.AsNoTracking().Include(u => u.Laboratory).FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return Problem(detail: "User not found", statusCode: StatusCodes.Status404NotFound);
        }

        return user.ToResponse();
    }

    /// <summary>Create a user with any role (Admin)</summary>
    /// <remarks>
    /// For a <c>Patient</c>, <c>dateOfBirth</c> (age 18–120) and <c>gender</c> are required: the reference norms of lab
    /// tests depend on them. <c>phoneNumber</c> is a Turkish number (+90 and 10 digits); <c>city</c> is one of the testing cities.
    /// </remarks>
    /// <response code="201">User created.</response>
    /// <response code="400">Validation failed; see <c>errors</c>.</response>
    /// <response code="409">A user with this email already exists.</response>
    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserResponse>> CreateUser(CreateUserRequest request)
    {
        // У пациента по полу и возрасту выбираются нормы анализов — без них аккаунт создавать нельзя
        if (request.Role == Roles.Patient)
        {
            if (request.DateOfBirth == null)
            {
                ModelState.AddModelError(nameof(CreateUserRequest.DateOfBirth), "Date of birth is required for a patient");
            }
            if (string.IsNullOrWhiteSpace(request.Gender))
            {
                ModelState.AddModelError(nameof(CreateUserRequest.Gender), "Gender is required for a patient");
            }
        }
        if (request.Role == Roles.LabStaff && !await LaboratoryExistsAsync(request.LaboratoryId))
        {
            ModelState.AddModelError(nameof(CreateUserRequest.LaboratoryId), "Select the laboratory of this staff member");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var email = UserMappings.NormalizeEmail(request.Email);

        if (await _context.Users.AnyAsync(u => u.Email == email))
        {
            return EmailConflict();
        }

        var user = new User
        {
            Email = email,
            FullName = request.FullName.Trim(),
            Role = request.Role,
            Height = request.Height,
            Weight = request.Weight,
            Gender = request.Gender?.Trim() ?? string.Empty,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? string.Empty : UserMappings.NormalizePhone(request.PhoneNumber),
            DateOfBirth = request.DateOfBirth,
            City = request.City ?? string.Empty,
            // Healthly ID — только пациентам; лаборатория — только сотрудникам лаборатории
            PatientCode = request.Role == Roles.Patient ? await PatientCodes.GenerateUniqueAsync(_context) : null,
            LaboratoryId = request.Role == Roles.LabStaff ? request.LaboratoryId : null,
        };
        user.PasswordHash = _passwords.Hash(user, request.Password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        await _context.Entry(user).Reference(u => u.Laboratory).LoadAsync();

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user.ToResponse());
    }

    /// <summary>Update a user profile</summary>
    /// <remarks>
    /// Full update (send all fields). Allowed for the user themselves and Admin.
    /// <c>role</c> and <c>password</c> can only be changed by Admin; send <c>null</c> to keep them.
    /// Changing the password ends all of the user's sessions (their refresh tokens stop working).
    /// <c>phoneNumber</c>, <c>dateOfBirth</c> and <c>city</c>: <c>null</c> (or omitted) keeps the current value;
    /// an empty string clears the phone or city. The date of birth and gender pick the reference norms of lab tests.
    /// </remarks>
    /// <response code="200">The updated user.</response>
    /// <response code="400">Validation failed; see <c>errors</c>.</response>
    /// <response code="403">Not the owner and not Admin, or a non-admin tried to change role/password.</response>
    /// <response code="404">User not found.</response>
    /// <response code="409">Another user already has this email.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UserResponse>> UpdateUser(int id, UpdateUserRequest request)
    {
        var isAdmin = User.IsInRole(Roles.Admin);

        if (!isAdmin && !IsSelf(id))
        {
            return Forbid();
        }

        if (!isAdmin && (request.Role != null || request.Password != null))
        {
            return Forbid();
        }

        var user = await _context.Users.Include(u => u.Laboratory).FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return Problem(detail: "User not found", statusCode: StatusCodes.Status404NotFound);
        }

        var email = UserMappings.NormalizeEmail(request.Email);

        if (email != user.Email && await _context.Users.AnyAsync(u => u.Email == email))
        {
            return EmailConflict();
        }

        user.Email = email;
        user.FullName = request.FullName.Trim();
        user.Height = request.Height;
        user.Weight = request.Weight;
        user.Gender = request.Gender?.Trim() ?? string.Empty;

        // null — не менять (старые клиенты эти поля не присылают); "" у телефона и города — очистить
        if (request.PhoneNumber != null)
        {
            user.PhoneNumber = request.PhoneNumber.Trim().Length == 0 ? string.Empty : UserMappings.NormalizePhone(request.PhoneNumber);
        }
        if (request.DateOfBirth != null)
        {
            user.DateOfBirth = request.DateOfBirth;
        }
        if (request.City != null)
        {
            user.City = request.City;
        }

        if (request.Role != null)
        {
            user.Role = request.Role;
        }
        if (isAdmin && request.LaboratoryId != null)
        {
            if (!await LaboratoryExistsAsync(request.LaboratoryId))
            {
                ModelState.AddModelError(nameof(UpdateUserRequest.LaboratoryId), "Laboratory not found");
                return ValidationProblem(ModelState);
            }
            user.LaboratoryId = request.LaboratoryId;
        }
        // Роль сменилась: сотруднику лаборатории нужна лаборатория, пациенту — Healthly ID
        if (user.Role == Roles.LabStaff && user.LaboratoryId == null)
        {
            ModelState.AddModelError(nameof(UpdateUserRequest.LaboratoryId), "Select the laboratory of this staff member");
            return ValidationProblem(ModelState);
        }
        if (user.Role != Roles.LabStaff)
        {
            user.LaboratoryId = null;
        }
        if (user.Role == Roles.Patient && user.PatientCode == null)
        {
            user.PatientCode = await PatientCodes.GenerateUniqueAsync(_context);
        }

        if (request.Password != null)
        {
            user.PasswordHash = _passwords.Hash(user, request.Password);
        }

        await _context.SaveChangesAsync();

        // Новый пароль — все сессии пользователя на других устройствах завершаются (refresh-токены отзываются)
        if (request.Password != null)
        {
            await _refreshTokens.RevokeAllAsync(user.Id);
        }

        // Лаборатория могла смениться — в ответе её актуальное название
        user.Laboratory = user.LaboratoryId == null ? null : await _context.Laboratories.FindAsync(user.LaboratoryId);
        return user.ToResponse();
    }

    /// <summary>Delete a user (Admin)</summary>
    /// <remarks>Also deletes the user's documents, test results and uploaded files. An admin cannot delete their own account.</remarks>
    /// <response code="204">User deleted.</response>
    /// <response code="400">An admin tried to delete their own account.</response>
    /// <response code="404">User not found.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteUser(int id)
    {
        if (IsSelf(id))
        {
            return Problem(detail: "You cannot delete your own account", statusCode: StatusCodes.Status400BadRequest);
        }

        // Имена файлов запоминаем заранее: документы и показатели удалятся каскадно в БД
        var storedFiles = await _context.MedicalDocuments
            .Where(d => d.UserId == id)
            .Select(d => d.FilePath)
            .ToListAsync();

        var deleted = await _context.Users.Where(u => u.Id == id).ExecuteDeleteAsync();

        if (deleted == 0)
        {
            return Problem(detail: "User not found", statusCode: StatusCodes.Status404NotFound);
        }

        foreach (var storedFile in storedFiles)
        {
            _storage.Delete(storedFile);
        }

        return NoContent();
    }

    private bool IsSelf(int id) => User.GetUserId() == id;

    private async Task<bool> LaboratoryExistsAsync(int? laboratoryId) =>
        laboratoryId != null && await _context.Laboratories.AnyAsync(l => l.Id == laboratoryId);

    private ObjectResult EmailConflict() =>
        Problem(detail: "A user with this email already exists", statusCode: StatusCodes.Status409Conflict);
}
