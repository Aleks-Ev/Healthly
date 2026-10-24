using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using HealthlyApi.Data;
using HealthlyApi.Dtos;
using HealthlyApi.Models;
using HealthlyApi.Services;

namespace HealthlyApi.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly PasswordService _passwords;
    private readonly TokenService _tokens;
    private readonly RefreshTokenService _refreshTokens;

    public AuthController(AppDbContext context, PasswordService passwords, TokenService tokens, RefreshTokenService refreshTokens)
    {
        _context = context;
        _passwords = passwords;
        _tokens = tokens;
        _refreshTokens = refreshTokens;
    }

    /// <summary>Register a new patient account</summary>
    /// <remarks>
    /// Public endpoint. Always creates a user with the <c>Patient</c> role and returns tokens (like login, including the
    /// cookie option for browsers), so the app can log the user in immediately.
    /// <c>dateOfBirth</c> is <c>YYYY-MM-DD</c> (the user must be 18–120 years old); <c>gender</c> is <c>Female</c>, <c>Male</c> or <c>Other</c>;
    /// <c>city</c> is one of: Istanbul, Ankara, Izmir, Antalya, Bursa, Adana, Gaziantep, Konya.
    /// </remarks>
    /// <response code="201">Account created; the response contains the access and refresh tokens and the user profile.</response>
    /// <response code="400">Validation failed; see <c>errors</c> for messages per field.</response>
    /// <response code="409">A user with this email already exists.</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        // Дата рождения: не в будущем, возраст от 18 до 120 лет
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var dateOfBirth = request.DateOfBirth!.Value;
        if (dateOfBirth > today.AddYears(-18) || dateOfBirth < today.AddYears(-120))
        {
            ModelState.AddModelError(nameof(RegisterRequest.DateOfBirth), "You must be at least 18 years old to register");
            return ValidationProblem(ModelState);
        }

        var email = UserMappings.NormalizeEmail(request.Email);

        if (await _context.Users.AnyAsync(u => u.Email == email))
        {
            return Problem(detail: "A user with this email already exists", statusCode: StatusCodes.Status409Conflict);
        }

        var accessCode = request.AccessCode?.Trim();
        var user = new User
        {
            Email = email,
            FullName = $"{request.FirstName.Trim()} {request.LastName.Trim()}",
            Role = Roles.Patient,
            Gender = request.Gender,
            PhoneNumber = UserMappings.NormalizePhone(request.PhoneNumber),
            DateOfBirth = dateOfBirth,
            City = request.City,
            AccessCode = string.IsNullOrEmpty(accessCode) ? null : accessCode,
            MarketingOptIn = request.MarketingOptIn,
            ConsentsAcceptedAt = DateTime.UtcNow,
        };
        user.PasswordHash = _passwords.Hash(user, request.Password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(Me), null, await CreateAuthResponseAsync(user, RefreshTokenCookie.IsRequested(Request)));
    }

    /// <summary>Log in with email and password</summary>
    /// <remarks>
    /// Returns an access token (<c>token</c>, a JWT valid for 15 minutes) and a refresh token (valid for 30 days).
    /// Send the access token as <c>Authorization: Bearer &lt;token&gt;</c> on every other request.
    /// When it expires (any request returns <c>401</c>), get a new pair with <c>POST /api/auth/refresh</c>.
    ///
    /// Browsers: send the header <c>X-Refresh-Token-Transport: cookie</c> (and use <c>credentials: "include"</c>) to get the
    /// refresh token as an httpOnly cookie instead of in the body; <c>refreshToken</c> in the response is then <c>null</c>.
    /// </remarks>
    /// <response code="200">Logged in.</response>
    /// <response code="400">Email or password is missing or malformed.</response>
    /// <response code="401">Invalid email or password.</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var email = UserMappings.NormalizeEmail(request.Email);
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);

        if (user == null || !_passwords.Verify(user, request.Password))
        {
            return Problem(detail: "Invalid email or password", statusCode: StatusCodes.Status401Unauthorized);
        }

        return await CreateAuthResponseAsync(user, RefreshTokenCookie.IsRequested(Request));
    }

    /// <summary>Get a new access token with a refresh token</summary>
    /// <remarks>
    /// Exchanges a refresh token for a new access token <b>and a new refresh token</b>; the old refresh token stops working
    /// (rotation), so always save the new one. Using an already exchanged refresh token again is treated as theft and
    /// ends all of the user's sessions. If several requests get <c>401</c> at once, refresh only once and retry them all.
    ///
    /// Mobile apps send <c>{ "refreshToken": "..." }</c>. Browsers send an empty body <c>{}</c> with
    /// <c>credentials: "include"</c>: the token is read from the httpOnly cookie and the new one is set as a cookie again.
    /// </remarks>
    /// <response code="200">New tokens.</response>
    /// <response code="401">The refresh token is missing, invalid, expired or already used. Log in again.</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request)
    {
        // Токен из тела (мобильные) или из cookie (браузер); ответ — тем же способом
        var useCookie = string.IsNullOrEmpty(request.RefreshToken);
        var refreshToken = useCookie ? RefreshTokenCookie.Read(Request) : request.RefreshToken;

        var result = refreshToken == null ? null : await _refreshTokens.RotateAsync(refreshToken);

        if (result?.User == null)
        {
            if (useCookie)
            {
                RefreshTokenCookie.Delete(HttpContext);
            }
            return Problem(detail: "The refresh token is missing, invalid or expired. Log in again.", statusCode: StatusCodes.Status401Unauthorized);
        }

        // Роль/имя могли поменяться — новый access-токен собирается из актуальных данных пользователя
        var (token, expiresAt) = _tokens.CreateToken(result.User);
        return new AuthResponse(token, expiresAt, Deliver(result.Token!, result.ExpiresAt, useCookie), result.ExpiresAt, result.User.ToResponse());
    }

    /// <summary>Log out</summary>
    /// <remarks>
    /// Revokes the refresh token (from the body, or from the cookie for browsers) and deletes the cookie.
    /// The current access token stays valid until it expires (at most 15 minutes), so also delete it on the device.
    /// Always returns <c>204</c>, even for an unknown token.
    /// </remarks>
    /// <response code="204">Logged out.</response>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(RefreshRequest request)
    {
        var refreshToken = string.IsNullOrEmpty(request.RefreshToken) ? RefreshTokenCookie.Read(Request) : request.RefreshToken;
        if (refreshToken != null)
        {
            await _refreshTokens.RevokeAsync(refreshToken);
        }

        RefreshTokenCookie.Delete(HttpContext);
        return NoContent();
    }

    /// <summary>Get the current user</summary>
    /// <remarks>Returns the profile of the user the token belongs to. Use it on app start to check that a saved token is still valid.</remarks>
    /// <response code="200">The current user.</response>
    /// <response code="401">The token is missing, invalid or expired, or the user was deleted.</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<UserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> Me()
    {
        var user = await FindCurrentUserAsync();

        // Токен валиден, но пользователь удалён
        if (user == null)
        {
            return Unauthorized();
        }

        return user.ToResponse();
    }

    /// <summary>Change your own password</summary>
    /// <remarks>
    /// Requires the current password. Ends all other sessions (every refresh token of the user is revoked)
    /// and returns a new token pair for this device — save it like after login.
    /// A wrong current password returns <c>400</c> (not <c>401</c>), so clients don't mistake it for an expired token.
    /// Browsers: send <c>X-Refresh-Token-Transport: cookie</c> to keep the refresh token in the cookie.
    /// </remarks>
    /// <response code="200">Password changed; new tokens.</response>
    /// <response code="400">Validation failed, the current password is wrong, or the new password equals the current one; see <c>errors</c>.</response>
    /// <response code="401">The access token is missing, invalid or expired.</response>
    [HttpPut("me/password")]
    [Authorize]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> ChangePassword(ChangePasswordRequest request)
    {
        var user = await FindCurrentUserAsync(tracking: true);

        if (user == null)
        {
            return Unauthorized();
        }

        if (!_passwords.Verify(user, request.CurrentPassword))
        {
            ModelState.AddModelError(nameof(ChangePasswordRequest.CurrentPassword), "The current password is incorrect");
            return ValidationProblem(ModelState);
        }

        if (request.NewPassword == request.CurrentPassword)
        {
            ModelState.AddModelError(nameof(ChangePasswordRequest.NewPassword), "The new password must be different from the current one");
            return ValidationProblem(ModelState);
        }

        user.PasswordHash = _passwords.Hash(user, request.NewPassword);
        await _context.SaveChangesAsync();

        // Все сессии (в том числе на других устройствах) завершаются, этому устройству выдаётся новая пара токенов
        await _refreshTokens.RevokeAllAsync(user.Id);
        return await CreateAuthResponseAsync(user, RefreshTokenCookie.IsRequested(Request));
    }

    private async Task<User?> FindCurrentUserAsync(bool tracking = false)
    {
        var userId = User.GetUserId();
        if (userId == null)
        {
            return null;
        }

        var users = tracking ? _context.Users : _context.Users.AsNoTracking();
        return await users.FirstOrDefaultAsync(u => u.Id == userId);
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(User user, bool useCookie)
    {
        var (token, expiresAt) = _tokens.CreateToken(user);
        var (refreshToken, refreshExpiresAt) = await _refreshTokens.IssueAsync(user);
        return new AuthResponse(token, expiresAt, Deliver(refreshToken, refreshExpiresAt, useCookie), refreshExpiresAt, user.ToResponse());
    }

    // Отдать refresh-токен клиенту: в httpOnly-cookie (тогда в теле null) или в теле ответа
    private string? Deliver(string refreshToken, DateTime expiresAt, bool useCookie)
    {
        if (!useCookie)
        {
            return refreshToken;
        }

        RefreshTokenCookie.Set(HttpContext, refreshToken, expiresAt);
        return null;
    }
}
