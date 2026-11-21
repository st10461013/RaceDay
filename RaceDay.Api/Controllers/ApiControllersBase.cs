using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// Enrolments: Participants enter categories, Organisers see who has entered their events.
[Route("api")]
public class EnrolmentsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// Enrols the logged-in Participant in a category of an event (Participant only).
    /// <remarks>
    /// No request body is needed: the participant comes from the session and the event from the category.
    /// The enrolment is Confirmed and receives a bib number. A participant can enter a category once (re-enrolling
    /// after cancelling reactivates the original enrolment), only until the event date has passed, and only while
    /// the category has spaces left.
    /// </remarks>
    /// <response code="201">Enrolment recorded, including the bib number.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not a Participant.</response>
    /// <response code="404">No category with that id.</response>
    /// <response code="409">Already enrolled, category full, or the event has already taken place.</response>
    [HttpPost("categories/{categoryId:int}/enrolments")]
    [SessionAuthorize(UserRole.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentResponse>> Enrol(int categoryId)
    {
        var category = await _db.Categories.AsNoTracking()
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.CategoryId == categoryId);

        if (category is null)
        {
            return NotFoundProblem($"Category {categoryId} was not found.");
        }

        if (category.Event.EventDate < DateOnly.FromDateTime(DateTime.Now))
        {
            return ConflictProblem("Enrolment is closed because this event has already taken place.");
        }

        var participantId = CurrentUserId;
        var existing = await _db.Enrolments
            .FirstOrDefaultAsync(e => e.CategoryId == categoryId && e.ParticipantId == participantId);

        if (existing is not null && existing.Status != EnrolmentStatus.Cancelled)
        {
            return ConflictProblem("You are already enrolled in this category.");
        }

        var enrolled = await _db.Enrolments.CountAsync(e => e.CategoryId == categoryId && e.Status != EnrolmentStatus.Cancelled);
        if (enrolled >= category.MaxParticipants)
        {
            return ConflictProblem("This category is full.");
        }

        Enrolment enrolment;
        if (existing is not null)
        {
            // Re-enrolling after a cancellation: the unique (participant, category) row is reused.
            enrolment = existing;
            enrolment.Status = EnrolmentStatus.Confirmed;
            enrolment.EnrolmentDate = DateTime.Now;
        }
        else
        {
            enrolment = new Enrolment
            {
                ParticipantId = participantId,
                CategoryId = categoryId,
                Status = EnrolmentStatus.Confirmed
            };
            _db.Enrolments.Add(enrolment);
        }

        try
        {
            await _db.SaveChangesAsync();

            // The bib number needs the generated id, so it is assigned after the first save.
            enrolment.BibNumber ??= $"{BibPrefix(category.Event.EventName)}-{10000 + enrolment.EnrolmentId}";
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return ConflictProblem("You are already enrolled in this category.");
        }

        var response = await _db.Enrolments.AsNoTracking()
            .Where(e => e.EnrolmentId == enrolment.EnrolmentId)
            .Select(EnrolmentResponse.Projection)
            .FirstAsync();

        return CreatedAtAction(nameof(GetMyEnrolments), null, response);
    }

    /// Lists the logged-in Participant's own enrolments, upcoming and past (Participant only).    /// <response code="200">The participant's enrolments, including cancelled ones (see status).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not a Participant.</response>
    [HttpGet("enrolments/me")]
    [SessionAuthorize(UserRole.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<EnrolmentResponse>>> GetMyEnrolments()
    {
        var participantId = CurrentUserId;
        var enrolments = await _db.Enrolments.AsNoTracking()
            .Where(e => e.ParticipantId == participantId)
            .OrderBy(e => e.Category.Event.EventDate).ThenBy(e => e.EnrolmentId)
            .Select(EnrolmentResponse.Projection)
            .ToListAsync();

        return Ok(enrolments);
    }

    /// Lists every participant enrolled in an event owned by the logged-in Organiser (Organiser only).
    /// <response code="200">The event's enrolments with participant, category, bib number and status.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No event with that id.</response>
    [HttpGet("events/{eventId:int}/enrolments")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EnrolmentResponse>>> GetEventEnrolments(int eventId)
    {
        var organiserId = await _db.Events.AsNoTracking()
            .Where(e => e.EventId == eventId)
            .Select(e => (int?)e.OrganiserId)
            .FirstOrDefaultAsync();

        if (organiserId is null)
        {
            return NotFoundProblem($"Event {eventId} was not found.");
        }

        if (organiserId != CurrentUserId)
        {
            return ForbiddenProblem("You can only view enrolments for events that you created.");
        }

        var enrolments = await _db.Enrolments.AsNoTracking()
            .Where(e => e.Category.EventId == eventId)
            .OrderBy(e => e.Category.CategoryName).ThenBy(e => e.EnrolmentDate)
            .Select(EnrolmentResponse.Projection)
            .ToListAsync();

        return Ok(enrolments);
    }

    /// Cancels the logged-in Participant's own enrolment (Participant only).
    /// <remarks>
    /// The enrolment is kept with status Cancelled, so history is preserved and the place is released.
    /// Cancelling an already cancelled enrolment succeeds. Not possible once a result has been recorded.
    /// </remarks>
    /// <response code="204">Enrolment cancelled.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not a Participant, or the enrolment belongs to someone else.</response>
    /// <response code="404">No enrolment with that id.</response>
    /// <response code="409">A result has already been recorded.</response>
    [HttpDelete("enrolments/{enrolmentId:int}")]
    [SessionAuthorize(UserRole.Participant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(int enrolmentId)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId);

        if (enrolment is null)
        {
            return NotFoundProblem($"Enrolment {enrolmentId} was not found.");
        }

        if (enrolment.ParticipantId != CurrentUserId)
        {
            return ForbiddenProblem("You can only cancel your own enrolments.");
        }

        if (enrolment.Result is not null)
        {
            return ConflictProblem("A result has already been recorded, so this enrolment cannot be cancelled.");
        }

        if (enrolment.Status != EnrolmentStatus.Cancelled)
        {
            enrolment.Status = EnrolmentStatus.Cancelled;
            await _db.SaveChangesAsync();
        }

        return NoContent();
    }

    /// Builds the bib prefix from the event's initials, e.g. "Cape Town Cycle Tour" gives "CTC".
    private static string BibPrefix(string eventName)
    {
        var letters = eventName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(word => char.IsLetter(word[0]))
            .Select(word => char.ToUpperInvariant(word[0]))
            .Take(3)
            .ToArray();

        return letters.Length == 0 ? "RD" : new string(letters);
    }
}
