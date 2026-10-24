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
        var query = _context.Users.AsNoTracking();

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

        var user = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
        {
            return Problem(detail: "User not found", statusCode: StatusCodes.Status404NotFound);
        }

        return user.ToResponse();
    }

    /// <summary>Create a user with any role (Admin)</summary>
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
        };
        user.PasswordHash = _passwords.Hash(user, request.Password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user.ToResponse());
    }

    /// <summary>Update a user profile</summary>
    /// <remarks>
    /// Full update (send all fields). Allowed for the user themselves and Admin.
    /// <c>role</c> and <c>password</c> can only be changed by Admin; send <c>null</c> to keep them.
    /// Changing the password ends all of the user's sessions (their refresh tokens stop working).
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

        var user = await _context.Users.FindAsync(id);

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

        if (request.Role != null)
        {
            user.Role = request.Role;
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

    private ObjectResult EmailConflict() =>
        Problem(detail: "A user with this email already exists", statusCode: StatusCodes.Status409Conflict);
}
