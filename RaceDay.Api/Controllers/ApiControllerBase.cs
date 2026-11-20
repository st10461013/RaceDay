using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Auth;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    /// <summary>Id of the user stored in the current session (0 when nobody is logged in).</summary>
    protected int CurrentUserId => HttpContext.Session.GetInt32(SessionKeys.UserId) ?? 0;

    /// <summary>Reads the session on endpoints that are public but behave differently for logged-in users.</summary>
    protected async Task<(int? UserId, UserRole? Role)> GetSessionAsync()
    {
        await HttpContext.Session.LoadAsync(HttpContext.RequestAborted);
        var id = HttpContext.Session.GetInt32(SessionKeys.UserId);
        var parsed = Enum.TryParse<UserRole>(HttpContext.Session.GetString(SessionKeys.Role), out var role);
        return (id, parsed ? role : null);
    }

    protected ObjectResult BadRequestProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest, title: "Bad request");

    protected ObjectResult UnauthorizedProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized");

    protected ObjectResult ForbiddenProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden");

    protected ObjectResult NotFoundProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status404NotFound, title: "Not found");

    protected ObjectResult ConflictProblem(string detail) =>
        Problem(detail: detail, statusCode: StatusCodes.Status409Conflict, title: "Conflict");
}
