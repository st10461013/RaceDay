using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Events: browsable by anyone, managed by the Organiser who owns them.</summary>
[Route("api/events")]
public class EventsController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IWeatherService _weather;

    public EventsController(RaceDayDbContext db, IWeatherService weather)
    {
        _db = db;
        _weather = weather;
    }

    /// <summary>Browses and searches upcoming events (Public).</summary>
    /// <remarks>
    /// Only upcoming events (today or later) are listed unless <c>includePast=true</c>. Optional filters:
    /// <c>location</c> (text in the location), <c>date</c> (yyyy-MM-dd), <c>type</c> (Run, Walk, Cycle),
    /// <c>search</c> (text in the name) and <c>mine=true</c> (an Organiser's own events; requires an Organiser login).
    /// Results are ordered by date.
    /// </remarks>
    /// <response code="200">The matching events.</response>
    /// <response code="401">mine=true without being logged in.</response>
    /// <response code="403">mine=true as a Participant.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<EventResponse>>> GetEvents(
        [FromQuery] string? location,
        [FromQuery] DateOnly? date,
        [FromQuery] EventType? type,
        [FromQuery] string? search,
        [FromQuery] bool mine = false,
        [FromQuery] bool includePast = false)
    {
        var query = _db.Events.AsNoTracking().AsQueryable();

        if (mine)
        {
            var (userId, role) = await GetSessionAsync();
            if (userId is null)
            {
                return UnauthorizedProblem("Log in as an Organiser to list your own events.");
            }

            if (role != UserRole.Organiser)
            {
                return ForbiddenProblem("Only Organisers can filter by their own events.");
            }

            query = query.Where(e => e.OrganiserId == userId.Value);
        }

        if (!includePast && date is null)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);
            query = query.Where(e => e.EventDate >= today);
        }

        if (date.HasValue)
        {
            query = query.Where(e => e.EventDate == date.Value);
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            var term = location.Trim();
            query = query.Where(e => e.Location.Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(e => e.EventName.Contains(term));
        }

        if (type.HasValue)
        {
            query = query.Where(e => e.EventType == type.Value);
        }

        var events = await query.OrderBy(e => e.EventDate).ThenBy(e => e.EventName)
            .Select(EventResponse.Projection).ToListAsync();
        return Ok(events);
    }

    /// <summary>Gets the full details of a single event (Public).</summary>
    /// <response code="200">The event.</response>
    /// <response code="404">No event with that id.</response>
    [HttpGet("{eventId:int}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> GetEvent(int eventId)
    {
        var ev = await LoadResponseAsync(eventId);
        return ev is null ? NotFoundProblem($"Event {eventId} was not found.") : Ok(ev);
    }

    /// <summary>Creates an event (Organiser only).</summary>
    /// <remarks>
    /// Expects eventName, eventDate (today or later), location, eventType (Run, Walk, Cycle) and distanceKM;
    /// description and weatherLocationCode are optional. The caller becomes the event's organiser.
    /// </remarks>
    /// <response code="201">Event created.</response>
    /// <response code="400">Validation failed, or the date is in the past.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser.</response>
    [HttpPost]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventResponse>> CreateEvent([FromBody] EventRequest request)
    {
        if (!Enum.IsDefined(request.EventType!.Value))
        {
            return BadRequestProblem("EventType must be Run, Walk or Cycle.");
        }

        if (request.EventDate!.Value < DateOnly.FromDateTime(DateTime.Now))
        {
            return BadRequestProblem("The event date cannot be in the past.");
        }

        var ev = new Event { OrganiserId = CurrentUserId };
        Apply(ev, request);

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        var response = await LoadResponseAsync(ev.EventId);
        return CreatedAtAction(nameof(GetEvent), new { eventId = ev.EventId }, response);
    }

    /// <summary>Updates an event owned by the logged-in Organiser.</summary>
    /// <remarks>Expects the same fields as when creating an event.</remarks>
    /// <response code="200">The updated event.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No event with that id.</response>
    [HttpPut("{eventId:int}")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> UpdateEvent(int eventId, [FromBody] EventRequest request)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null)
        {
            return NotFoundProblem($"Event {eventId} was not found.");
        }

        if (ev.OrganiserId != CurrentUserId)
        {
            return ForbiddenProblem("You can only modify events that you created.");
        }

        if (!Enum.IsDefined(request.EventType!.Value))
        {
            return BadRequestProblem("EventType must be Run, Walk or Cycle.");
        }

        Apply(ev, request);
        await _db.SaveChangesAsync();

        return Ok(await LoadResponseAsync(eventId));
    }

    /// <summary>Deletes an event owned by the logged-in Organiser.</summary>
    /// <remarks>Also removes the event's routes, categories, enrolments and results.</remarks>
    /// <response code="204">Event deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No event with that id.</response>
    [HttpDelete("{eventId:int}")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteEvent(int eventId)
    {
        var ev = await _db.Events
            .Include(e => e.Routes)
            .Include(e => e.Categories).ThenInclude(c => c.Enrolments).ThenInclude(en => en.Result)
            .FirstOrDefaultAsync(e => e.EventId == eventId);

        if (ev is null)
        {
            return NotFoundProblem($"Event {eventId} was not found.");
        }

        if (ev.OrganiserId != CurrentUserId)
        {
            return ForbiddenProblem("You can only delete events that you created.");
        }

        // The Part 1 foreign keys do not cascade, so remove the children explicitly.
        var enrolments = ev.Categories.SelectMany(c => c.Enrolments).ToList();
        _db.Results.RemoveRange(enrolments.Where(en => en.Result != null).Select(en => en.Result!));
        _db.Enrolments.RemoveRange(enrolments);
        _db.Categories.RemoveRange(ev.Categories);
        _db.Routes.RemoveRange(ev.Routes);
        _db.Events.Remove(ev);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Gets the live weather forecast for an event's location (Organiser or Participant).</summary>
    /// <remarks>
    /// Uses the event's weatherLocationCode (e.g. ZA-CT). Forecasts are available up to 15 days ahead;
    /// for later dates the current conditions at the location are returned.
    /// </remarks>
    /// <response code="200">location, forecastDate, temperature (°C), conditions and windSpeed (km/h).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">No such event, or the event has no usable weather location code.</response>
    /// <response code="503">The weather service could not be reached.</response>
    [HttpGet("{eventId:int}/weather")]
    [SessionAuthorize(UserRole.Organiser, UserRole.Participant)]
    [ProducesResponseType(typeof(WeatherResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<WeatherResponse>> GetWeather(int eventId, CancellationToken cancellationToken)
    {
        var ev = await _db.Events.AsNoTracking()
            .Where(e => e.EventId == eventId)
            .Select(e => new { e.EventDate, e.WeatherLocationCode })
            .FirstOrDefaultAsync(cancellationToken);

        if (ev is null)
        {
            return NotFoundProblem($"Event {eventId} was not found.");
        }

        if (string.IsNullOrWhiteSpace(ev.WeatherLocationCode))
        {
            return NotFoundProblem("This event has no weather location code.");
        }

        try
        {
            var forecast = await _weather.GetForecastAsync(ev.WeatherLocationCode, ev.EventDate, cancellationToken);
            return forecast is null
                ? NotFoundProblem($"The weather location code '{ev.WeatherLocationCode}' is not recognised.")
                : Ok(forecast);
        }
        catch (WeatherUnavailableException)
        {
            return Problem(
                detail: "The weather service is currently unavailable. Please try again later.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Weather unavailable");
        }
    }

    private static void Apply(Event ev, EventRequest r)
    {
        ev.EventName = r.EventName.Trim();
        ev.EventDate = r.EventDate!.Value;
        ev.Location = r.Location.Trim();
        ev.Description = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        ev.WeatherLocationCode = string.IsNullOrWhiteSpace(r.WeatherLocationCode) ? null : r.WeatherLocationCode.Trim();
        ev.EventType = r.EventType!.Value;
        ev.DistanceKM = r.DistanceKM!.Value;
    }

    private Task<EventResponse?> LoadResponseAsync(int eventId) =>
        _db.Events.AsNoTracking()
            .Where(e => e.EventId == eventId)
            .Select(EventResponse.Projection)
            .FirstOrDefaultAsync();
}
