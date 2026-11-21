using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

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
}
