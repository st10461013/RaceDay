using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Route (course) information for an event: public to read, managed by the event's Organiser.</summary>
[Route("api")]
public class RoutesController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public RoutesController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// <summary>Lists the route information for an event (Public).</summary>
    /// <response code="200">The event's routes.</response>
    /// <response code="404">No event with that id.</response>
    [HttpGet("events/{eventId:int}/routes")]
    [ProducesResponseType(typeof(IEnumerable<RouteResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<RouteResponse>>> GetRoutes(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
        {
            return NotFoundProblem($"Event {eventId} was not found.");
        }

        var routes = await _db.Routes.AsNoTracking()
            .Where(r => r.EventId == eventId)
            .OrderBy(r => r.RouteName)
            .Select(RouteResponse.Projection)
            .ToListAsync();

        return Ok(routes);
    }

    /// <summary>Gets one route (Public).</summary>
    /// <response code="200">The route.</response>
    /// <response code="404">No route with that id.</response>
    [HttpGet("routes/{routeId:int}")]
    [ProducesResponseType(typeof(RouteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteResponse>> GetRoute(int routeId)
    {
        var route = await _db.Routes.AsNoTracking()
            .Where(r => r.RouteId == routeId)
            .Select(RouteResponse.Projection)
            .FirstOrDefaultAsync();

        return route is null ? NotFoundProblem($"Route {routeId} was not found.") : Ok(route);
    }

    /// <summary>Adds a route to an event owned by the logged-in Organiser.</summary>
    /// <remarks>Expects routeName and distanceKM; elevationGainM (default 0) and mapURL are optional.</remarks>
    /// <response code="201">Route created.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No event with that id.</response>
    [HttpPost("events/{eventId:int}/routes")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(RouteResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteResponse>> CreateRoute(int eventId, [FromBody] RouteRequest request)
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
            return ForbiddenProblem("You can only add routes to events that you created.");
        }

        var route = new RaceRoute { EventId = eventId };
        Apply(route, request);

        _db.Routes.Add(route);
        await _db.SaveChangesAsync();

        var response = await _db.Routes.AsNoTracking()
            .Where(r => r.RouteId == route.RouteId)
            .Select(RouteResponse.Projection)
            .FirstAsync();

        return CreatedAtAction(nameof(GetRoute), new { routeId = route.RouteId }, response);
    }

    /// <summary>Updates a route belonging to one of the Organiser's events.</summary>
    /// <response code="200">The updated route.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No route with that id.</response>
    [HttpPut("routes/{routeId:int}")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(RouteResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RouteResponse>> UpdateRoute(int routeId, [FromBody] RouteRequest request)
    {
        var route = await _db.Routes.Include(r => r.Event).FirstOrDefaultAsync(r => r.RouteId == routeId);
        if (route is null)
        {
            return NotFoundProblem($"Route {routeId} was not found.");
        }

        if (route.Event.OrganiserId != CurrentUserId)
        {
            return ForbiddenProblem("You can only change routes of events that you created.");
        }

        Apply(route, request);
        await _db.SaveChangesAsync();

        var response = await _db.Routes.AsNoTracking()
            .Where(r => r.RouteId == routeId)
            .Select(RouteResponse.Projection)
            .FirstAsync();

        return Ok(response);
    }

    /// <summary>Deletes a route belonging to one of the Organiser's events.</summary>
    /// <response code="204">Route deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No route with that id.</response>
    [HttpDelete("routes/{routeId:int}")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRoute(int routeId)
    {
        var route = await _db.Routes.Include(r => r.Event).FirstOrDefaultAsync(r => r.RouteId == routeId);
        if (route is null)
        {
            return NotFoundProblem($"Route {routeId} was not found.");
        }

        if (route.Event.OrganiserId != CurrentUserId)
        {
            return ForbiddenProblem("You can only delete routes of events that you created.");
        }

        _db.Routes.Remove(route);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static void Apply(RaceRoute route, RouteRequest r)
    {
        route.RouteName = r.RouteName.Trim();
        route.DistanceKM = r.DistanceKM!.Value;
        route.ElevationGainM = r.ElevationGainM ?? 0;
        route.MapURL = string.IsNullOrWhiteSpace(r.MapURL) ? null : r.MapURL.Trim();
    }
}
