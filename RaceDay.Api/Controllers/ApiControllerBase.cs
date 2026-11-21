using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Auth;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected int CurrentUserId =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected ActionResult BadRequestProblem(string detail) =>
        Problem(detail, statusCode: StatusCodes.Status400BadRequest);

    protected ActionResult ForbiddenProblem(string detail) =>
        Problem(detail, statusCode: StatusCodes.Status403Forbidden);

    protected ActionResult ConflictProblem(string detail) =>
        Problem(detail, statusCode: StatusCodes.Status409Conflict);

    protected ActionResult NotFoundProblem(string detail) =>
        Problem(detail, statusCode: StatusCodes.Status404NotFound);

    protected ActionResult UnauthorizedProblem(string detail) =>
        Problem(detail, statusCode: StatusCodes.Status401Unauthorized);

    protected async Task<(int? UserId, UserRole Role)> GetSessionAsync()
    {
        await HttpContext.Session.LoadAsync(HttpContext.RequestAborted);

        var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
        var roleText = HttpContext.Session.GetString(SessionKeys.Role);

        if (userId is null || !Enum.TryParse<UserRole>(roleText, out var role))
        {
            return (null, default);
        }

        return (userId, role);
    }
}
