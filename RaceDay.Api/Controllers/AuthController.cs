using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Registration, login and logout.</summary>
[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IConfiguration _config;

    public AuthController(RaceDayDbContext db, IPasswordHasher hasher, IConfiguration config)
    {
        _db = db;
        _hasher = hasher;
        _config = config;
    }

    /// <summary>Registers a new Organiser or Participant account (Public).</summary>
    /// <remarks>
    /// Expects role, firstName, lastName, email, password and optionally phoneNumber. Participants register freely.
    /// Organiser accounts can create and manage events, so they additionally need the organiser access code.
    /// The password is stored only as a salted PBKDF2 hash.
    /// </remarks>
    /// <response code="201">Account created. Returns the new user object (never the password).</response>
    /// <response code="400">Validation failed (missing fields, invalid email, weak password, invalid role).</response>
    /// <response code="403">Registering as Organiser without a valid access code.</response>
    /// <response code="409">An account with this email already exists.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserResponse>> Register([FromBody] RegisterRequest request)
    {
        var role = request.Role!.Value;
        if (!Enum.IsDefined(role))
        {
            return BadRequestProblem("Role must be either Organiser or Participant.");
        }

        if (role == UserRole.Organiser && !HasValidOrganiserCode(request.OrganiserAccessCode))
        {
            return ForbiddenProblem("A valid organiser access code is required to register as an Organiser.");
        }

        var email = NormaliseEmail(request.Email);
        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            return ConflictProblem("An account with this email address already exists.");
        }

        var user = new AppUser
        {
            Role = role,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim()
        };

        _db.Users.Add(user);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Lost a race against another registration with the same email (unique index).
            return ConflictProblem("An account with this email address already exists.");
        }

        return Created("/api/users/me", UserResponse.From(user));
    }

    /// <summary>Logs a user in and starts a session (Public).</summary>
    /// <remarks>
    /// Expects email and password. On success the user id and role are stored in the server-side session and a
    /// session cookie is returned; later requests are authenticated by that cookie. (The Part 1 plan issued a JWT;
    /// the Part 2 brief requires session management, so the response is the user object without a token.)
    /// </remarks>
    /// <response code="200">Logged in. Returns the user object including the role.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Email or password is incorrect.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> Login([FromBody] LoginRequest request)
    {
        var email = NormaliseEmail(request.Email);
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email);

        // One generic message so the API does not reveal which emails are registered.
        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
        {
            return UnauthorizedProblem("Invalid email or password.");
        }

        // Start from a clean session to avoid session fixation.
        HttpContext.Session.Clear();
        HttpContext.Session.SetInt32(SessionKeys.UserId, user.UserId);
        HttpContext.Session.SetString(SessionKeys.Role, user.Role.ToString());

        return Ok(UserResponse.From(user));
    }

    /// <summary>Logs the current user out and ends the session (Organiser or Participant).</summary>
    /// <response code="204">Session ended.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPost("logout")]
    [SessionAuthorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return NoContent();
    }

    private bool HasValidOrganiserCode(string? supplied)
    {
        var expected = _config["Auth:OrganiserAccessCode"];
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrEmpty(supplied))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(supplied),
            Encoding.UTF8.GetBytes(expected));
    }

    private static string NormaliseEmail(string email) => email.Trim().ToLowerInvariant();
}
