using System.Text.Json;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>Live verification that the Swagger document exposes every endpoint .</summary>
public class SwaggerTests : ApiTestBase, IClassFixture<RaceDayFactory>
{
    public SwaggerTests(RaceDayFactory factory) : base(factory)
    {
    }

    public async Task SwaggerDocument_IsPublic_AndListsEveryEndpointFromThePart1Plan()
    {
        var response = await NewClient().GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        // (route, HTTP method) .
        (string Route, string Method)[] expected =
        {
            ("/api/auth/register", "post"),
            ("/api/auth/login", "post"),
            ("/api/auth/logout", "post"),
            ("/api/users/me", "get"),
            ("/api/users/me", "put"),
            ("/api/events", "get"),
            ("/api/events", "post"),
            ("/api/events/{eventId}", "get"),
            ("/api/events/{eventId}", "put"),
            ("/api/events/{eventId}", "delete"),
            ("/api/events/{eventId}/weather", "get"),
            ("/api/events/{eventId}/routes", "get"),
            ("/api/events/{eventId}/routes", "post"),
            ("/api/routes/{routeId}", "put"),
            ("/api/routes/{routeId}", "delete"),
            ("/api/events/{eventId}/categories", "get"),
            ("/api/events/{eventId}/categories", "post"),
            ("/api/categories/{categoryId}", "put"),
            ("/api/categories/{categoryId}", "delete"),
            ("/api/categories/{categoryId}/enrolments", "post"),
            ("/api/enrolments/me", "get"),
            ("/api/events/{eventId}/enrolments", "get"),
            ("/api/enrolments/{enrolmentId}", "delete"),
            ("/api/enrolments/{enrolmentId}/result", "post"),
            ("/api/results/{resultId}", "put"),
            ("/api/events/{eventId}/results", "get"),
            ("/api/results/me", "get")
        };

        foreach (var (route, method) in expected)
        {
            Assert.True(paths.TryGetProperty(route, out var item), $"Swagger is missing {route}");
            Assert.True(item.TryGetProperty(method, out _), $"Swagger is missing {method.ToUpperInvariant()} {route}");
        }
    }

    public async Task SwaggerEndpoints_HaveDescriptions_AndDocumentedResponses()
    {
        var response = await NewClient().GetAsync("/swagger/v1/swagger.json");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        var login = document.RootElement.GetProperty("paths").GetProperty("/api/auth/login").GetProperty("post");

        Assert.False(string.IsNullOrWhiteSpace(login.GetProperty("summary").GetString()));
        Assert.True(login.TryGetProperty("requestBody", out _));
        Assert.True(login.TryGetProperty("responses", out var responses));
        Assert.True(responses.TryGetProperty("200", out _));
        Assert.True(responses.TryGetProperty("401", out _));
    }

    public async Task SwaggerUi_IsServed()
    {
        var response = await NewClient().GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
