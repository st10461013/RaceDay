using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Results: Organisers capture them, the event leaderboard is public, Participants see their own history.</summary>
[Route("api")]
public class ResultsController : ApiControllerBase
{
    private static readonly TimeSpan MaxFinishTime = TimeSpan.FromHours(24);

    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Captures a participant's result for an enrolment (Organiser who owns the event).</summary>
    /// <remarks>
    /// Expects finishTime (hh:mm:ss), position and status (Finished, DNF or DSQ; default Finished).
    /// A Finished result needs both finishTime and position. A DNF or DSQ result has no position.
    /// Each enrolment has at most one result, and a position can be used once per category.
    /// </remarks>
    /// <response code="201">Result recorded.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No enrolment with that id.</response>
    /// <response code="409">The enrolment already has a result, is cancelled, or the position is taken in that category.</response>
    [HttpPost("enrolments/{enrolmentId:int}/result")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultResponse>> CaptureResult(int enrolmentId, [FromBody] ResultRequest request)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Category).ThenInclude(c => c.Event)
            .Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId);

        if (enrolment is null)
        {
            return NotFoundProblem($"Enrolment {enrolmentId} was not found.");
        }

        if (enrolment.Category.Event.OrganiserId != CurrentUserId)
        {
            return ForbiddenProblem("You can only capture results for events that you created.");
        }

        var status = request.Status ?? ResultStatus.Finished;
        var invalid = Validate(status, request.FinishTime, request.Position);
        if (invalid is not null)
        {
            return BadRequestProblem(invalid);
        }

        if (enrolment.Status == EnrolmentStatus.Cancelled)
        {
            return ConflictProblem("This enrolment was cancelled, so a result cannot be recorded.");
        }

        if (enrolment.Result is not null)
        {
            return ConflictProblem("A result has already been recorded for this enrolment. Use PUT /api/results/{resultId} to change it.");
        }

        if (request.Position is { } position && await PositionTakenAsync(enrolment.CategoryId, position, excludeResultId: null))
        {
            return ConflictProblem($"Position {position} is already taken in this category.");
        }

        var result = new EventResult
        {
            EnrolmentId = enrolmentId,
            FinishTime = request.FinishTime,
            Position = request.Position,
            Status = status
        };

        _db.Results.Add(result);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ConflictProblem("A result has already been recorded for this enrolment.");
        }

        var response = await LoadResponseAsync(result.ResultId);
        return CreatedAtAction(nameof(GetMyResults), null, response);
    }

    /// <summary>Updates or corrects a captured result (Organiser who owns the event).</summary>
    /// <remarks>Expects the same fields as when capturing a result.</remarks>
    /// <response code="200">The updated result.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No result with that id.</response>
    /// <response code="409">The position is already taken in that category.</response>
    [HttpPut("results/{resultId:int}")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultResponse>> UpdateResult(int resultId, [FromBody] ResultRequest request)
    {
        var result = await _db.Results
            .Include(r => r.Enrolment).ThenInclude(e => e.Category).ThenInclude(c => c.Event)
            .FirstOrDefaultAsync(r => r.ResultId == resultId);

        if (result is null)
        {
            return NotFoundProblem($"Result {resultId} was not found.");
        }

        if (result.Enrolment.Category.Event.OrganiserId != CurrentUserId)
        {
            return ForbiddenProblem("You can only change results for events that you created.");
        }

        var status = request.Status ?? ResultStatus.Finished;
        var invalid = Validate(status, request.FinishTime, request.Position);
        if (invalid is not null)
        {
            return BadRequestProblem(invalid);
        }

        if (request.Position is { } position &&
            await PositionTakenAsync(result.Enrolment.CategoryId, position, excludeResultId: resultId))
        {
            return ConflictProblem($"Position {position} is already taken in this category.");
        }

        result.FinishTime = request.FinishTime;
        result.Position = request.Position;
        result.Status = status;
        await _db.SaveChangesAsync();

        return Ok(await LoadResponseAsync(resultId));
    }

    /// <summary>Views the full results (leaderboard) for an event (Public).</summary>
    /// <response code="200">Results ordered by category, then finishing position.</response>
    /// <response code="404">No event with that id.</response>
    [HttpGet("events/{eventId:int}/results")]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ResultResponse>>> GetEventResults(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
        {
            return NotFoundProblem($"Event {eventId} was not found.");
        }

        var results = await _db.Results.AsNoTracking()
            .Where(r => r.Enrolment.Category.EventId == eventId)
            .OrderBy(r => r.Enrolment.Category.CategoryName)
            .ThenBy(r => r.Position ?? int.MaxValue)
            .Select(ResultResponse.Projection)
            .ToListAsync();

        return Ok(results);
    }

    /// <summary>Views the logged-in Participant's personal performance history across all past events (Participant only).</summary>
    /// <response code="200">The participant's results with event and category context; never other participants' results.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not a Participant.</response>
    [HttpGet("results/me")]
    [SessionAuthorize(UserRole.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<ResultResponse>>> GetMyResults()
    {
        var participantId = CurrentUserId;
        var results = await _db.Results.AsNoTracking()
            .Where(r => r.Enrolment.ParticipantId == participantId)
            .OrderByDescending(r => r.Enrolment.Category.Event.EventDate)
            .Select(ResultResponse.Projection)
            .ToListAsync();

        return Ok(results);
    }

    private static string? Validate(ResultStatus status, TimeSpan? finishTime, int? position)
    {
        if (!Enum.IsDefined(status))
        {
            return "Status must be Finished, DNF or DSQ.";
        }

        if (finishTime is { } time && (time <= TimeSpan.Zero || time >= MaxFinishTime))
        {
            return "FinishTime must be greater than 00:00:00 and less than 24 hours.";
        }

        if (status == ResultStatus.Finished)
        {
            return finishTime is null || position is null
                ? "A Finished result needs both FinishTime and Position."
                : null;
        }

        return position is not null ? "A DNF or DSQ result cannot have a finishing position." : null;
    }

    private Task<bool> PositionTakenAsync(int categoryId, int position, int? excludeResultId) =>
        _db.Results.AnyAsync(r =>
            r.Enrolment.CategoryId == categoryId &&
            r.Position == position &&
            (excludeResultId == null || r.ResultId != excludeResultId));

    private Task<ResultResponse?> LoadResponseAsync(int resultId) =>
        _db.Results.AsNoTracking()
            .Where(r => r.ResultId == resultId)
            .Select(ResultResponse.Projection)
            .FirstOrDefaultAsync();
}
