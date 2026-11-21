using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Controllers;

/// <summary>View and update the logged-in user's own profile (Organiser or Participant).</summary>
[Route("api/users/me")]
public class UsersController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public UsersController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Returns the logged-in user's own profile (Organiser or Participant).</summary>
    /// <response code="200">The current user's profile.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet]
    [SessionAuthorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> GetMe()
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null)
        {
            HttpContext.Session.Clear();
            return UnauthorizedProblem("Your account no longer exists. Please log in again.");
        }

        return Ok(UserResponse.From(user));
    }

    /// <summary>Updates the logged-in user's own profile (Organiser or Participant).</summary>
    /// <remarks>Expects firstName, lastName and optionally phoneNumber. Email and role cannot be changed.</remarks>
    /// <response code="200">The updated user object.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPut]
    [SessionAuthorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserResponse>> UpdateMe([FromBody] UpdateProfileRequest request)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == CurrentUserId);
        if (user is null)
        {
            HttpContext.Session.Clear();
            return UnauthorizedProblem("Your account no longer exists. Please log in again.");
        }

        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();

        await _db.SaveChangesAsync();
        return Ok(UserResponse.From(user));
    }
}
