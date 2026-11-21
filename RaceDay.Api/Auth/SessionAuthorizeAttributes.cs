using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RaceDay.Api.Models;

namespace RaceDay.Api.Auth;

public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
}


/// Session-based authorisation. Requires a logged-in session (401 otherwise) and,
/// when roles are supplied, that the session role is one of them (403 otherwise).
/// Apply on a controller for "any authenticated user" and on an action to narrow by role.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class SessionAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly UserRole[] _roles;

    public SessionAuthorizeAttribute(params UserRole[] roles)
    {
        _roles = roles;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        await session.LoadAsync(context.HttpContext.RequestAborted);

        var userId = session.GetInt32(SessionKeys.UserId);
        var roleText = session.GetString(SessionKeys.Role);

        if (userId is null || !Enum.TryParse<UserRole>(roleText, out var role))
        {
            context.Result = Problem(StatusCodes.Status401Unauthorized, "Authentication required",
                "You must log in before accessing this resource.");
            return;
        }

        if (_roles.Length > 0 && !_roles.Contains(role))
        {
            context.Result = Problem(StatusCodes.Status403Forbidden, "Forbidden",
                $"This action requires the {string.Join(" or ", _roles)} role.");
        }
    }

    private static ObjectResult Problem(int status, string title, string detail) =>
        new(new ProblemDetails { Status = status, Title = title, Detail = detail }) { StatusCode = status };
}
