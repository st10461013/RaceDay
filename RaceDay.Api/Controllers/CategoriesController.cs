using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// Categories (the individual races within an event, e.g. "Under 20", "Senior", "10km", "21km"):
/// public to read, managed by the event's Organiser.
[Route("api")]
public class CategoriesController : ApiControllerBase
{
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db)
    {
        _db = db;
    }

    /// Lists all categories for an event (Public).
    /// <response code="200">The event's categories, each with the number of enrolled participants.</response>
    /// <response code="404">No event with that id.</response>
    [HttpGet("events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(IEnumerable<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<CategoryResponse>>> GetCategories(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
        {
            return NotFoundProblem($"Event {eventId} was not found.");
        }

        var categories = await _db.Categories.AsNoTracking()
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.CategoryName)
            .Select(CategoryResponse.Projection)
            .ToListAsync();

        return Ok(categories);
    }

    /// Gets one category (Public).
    /// <response code="200">The category.</response>
    /// <response code="404">No category with that id.</response>
    [HttpGet("categories/{categoryId:int}")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryResponse>> GetCategory(int categoryId)
    {
        var category = await LoadResponseAsync(categoryId);
        return category is null ? NotFoundProblem($"Category {categoryId} was not found.") : Ok(category);
    }

    /// Adds a category to an event owned by the logged-in Organiser.
    /// <remarks>
    /// Expects categoryName and distanceKM; maxParticipants (default 100) and entryFee (default 0) are optional.
    /// Names must be unique within an event.
    /// </remarks>
    /// <response code="201">Category created.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No event with that id.</response>
    /// <response code="409">A category with that name already exists for the event.</response>
    [HttpPost("events/{eventId:int}/categories")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> CreateCategory(int eventId, [FromBody] CategoryRequest request)
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
            return ForbiddenProblem("You can only add categories to events that you created.");
        }

        var name = request.CategoryName.Trim();
        if (await _db.Categories.AnyAsync(c => c.EventId == eventId && c.CategoryName.ToLower() == name.ToLower()))
        {
            return ConflictProblem($"A category named '{name}' already exists for this event.");
        }

        var category = new Category { EventId = eventId };
        Apply(category, request);

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        var response = await LoadResponseAsync(category.CategoryId);
        return CreatedAtAction(nameof(GetCategory), new { categoryId = category.CategoryId }, response);
    }

    /// Updates a category belonging to one of the Organiser's events.
    /// <remarks>
    /// maxParticipants cannot be lowered below the number of participants already enrolled.
    /// </remarks>
    /// <response code="200">The updated category.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No category with that id.</response>
    /// <response code="409">Name already used in the event, or capacity below the current enrolments.</response>
    [HttpPut("categories/{categoryId:int}")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryResponse>> UpdateCategory(int categoryId, [FromBody] CategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == categoryId);
        if (category is null)
        {
            return NotFoundProblem($"Category {categoryId} was not found.");
        }

        if (category.Event.OrganiserId != CurrentUserId)
        {
            return ForbiddenProblem("You can only change categories of events that you created.");
        }

        var name = request.CategoryName.Trim();
        if (await _db.Categories.AnyAsync(c =>
                c.EventId == category.EventId && c.CategoryId != categoryId && c.CategoryName.ToLower() == name.ToLower()))
        {
            return ConflictProblem($"A category named '{name}' already exists for this event.");
        }

        var capacity = request.MaxParticipants ?? 100;
        var enrolled = await _db.Enrolments.CountAsync(e => e.CategoryId == categoryId && e.Status != EnrolmentStatus.Cancelled);
        if (capacity < enrolled)
        {
            return ConflictProblem($"{enrolled} participants are already enrolled, so MaxParticipants cannot be below {enrolled}.");
        }

        Apply(category, request);
        await _db.SaveChangesAsync();

        return Ok(await LoadResponseAsync(categoryId));
    }

    /// Deletes a category belonging to one of the Organiser's events.
    /// <remarks>A category that has enrolments cannot be deleted.</remarks>
    /// <response code="204">Category deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Caller is not an Organiser, or the event belongs to another Organiser.</response>
    /// <response code="404">No category with that id.</response>
    /// <response code="409">Participants have enrolled in this category.</response>
    [HttpDelete("categories/{categoryId:int}")]
    [SessionAuthorize(UserRole.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteCategory(int categoryId)
    {
        var category = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == categoryId);
        if (category is null)
        {
            return NotFoundProblem($"Category {categoryId} was not found.");
        }

        if (category.Event.OrganiserId != CurrentUserId)
        {
            return ForbiddenProblem("You can only delete categories of events that you created.");
        }

        if (await _db.Enrolments.AnyAsync(e => e.CategoryId == categoryId))
        {
            return ConflictProblem("Participants have enrolled in this category, so it cannot be deleted.");
        }

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static void Apply(Category category, CategoryRequest r)
    {
        category.CategoryName = r.CategoryName.Trim();
        category.DistanceKM = r.DistanceKM!.Value;
        category.MaxParticipants = r.MaxParticipants ?? 100;
        category.EntryFee = r.EntryFee ?? 0m;
    }

    private Task<CategoryResponse?> LoadResponseAsync(int categoryId) =>
        _db.Categories.AsNoTracking()
            .Where(c => c.CategoryId == categoryId)
            .Select(CategoryResponse.Projection)
            .FirstOrDefaultAsync();
}
