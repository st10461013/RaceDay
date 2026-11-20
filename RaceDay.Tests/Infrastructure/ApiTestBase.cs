using System.Text.Json;
using System.Text.Json.Serialization;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Tests.Infrastructure;

public static class Json
{
    /// <summary>Same settings as the API: camelCase properties and enums as strings.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}

/// <summary>Shared helpers for building scenarios (users, events, categories, enrolments) through the public API.</summary>
public abstract class ApiTestBase
{
    protected const string Password = "Passw0rd!";

    protected ApiTestBase(RaceDayFactory factory)
    {
        Factory = factory;
    }

    protected RaceDayFactory Factory { get; }

    /// <summary>A new client with its own cookie jar, i.e. its own browser session.</summary>
    protected HttpClient NewClient() => Factory.CreateClient();

    protected static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@example.com";

    protected static Task<HttpResponseMessage> PostAsync(HttpClient client, string url, object? body = null) =>
        client.PostAsJsonAsync(url, body ?? new { }, Json.Options);

    protected static Task<HttpResponseMessage> PutAsync(HttpClient client, string url, object body) =>
        client.PutAsJsonAsync(url, body, Json.Options);

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json.Options))!;

    protected static Task<HttpResponseMessage> RegisterAsync(
        HttpClient client, string email, UserRole role, string? organiserCode = null, string password = Password) =>
        PostAsync(client, "/api/auth/register", new
        {
            role,
            firstName = "Test",
            lastName = role.ToString(),
            email,
            password,
            phoneNumber = "0821234567",
            organiserAccessCode = organiserCode
        });

    protected static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, string password = Password) =>
        PostAsync(client, "/api/auth/login", new { email, password });

    protected async Task<(HttpClient Client, UserResponse User)> CreateOrganiserAsync()
    {
        var client = NewClient();
        var email = UniqueEmail("organiser");

        var registered = await RegisterAsync(client, email, UserRole.Organiser, RaceDayFactory.OrganiserCode);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        var login = await LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        return (client, await ReadAsync<UserResponse>(login));
    }

    protected async Task<(HttpClient Client, UserResponse User)> CreateParticipantAsync()
    {
        var client = NewClient();
        var email = UniqueEmail("participant");

        var registered = await RegisterAsync(client, email, UserRole.Participant);
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        var login = await LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        return (client, await ReadAsync<UserResponse>(login));
    }

    protected static object EventPayload(
        string? name = null, EventType type = EventType.Run, string? weatherCode = "ZA-CT", int daysAhead = 30) => new
    {
        eventName = name ?? $"City 10km {Guid.NewGuid():N}",
        eventDate = DateOnly.FromDateTime(DateTime.Now.AddDays(daysAhead)),
        location = "Cape Town, Western Cape",
        description = "A friendly road race through the city.",
        weatherLocationCode = weatherCode,
        eventType = type,
        distanceKM = 10
    };

    protected async Task<EventResponse> CreateEventAsync(HttpClient organiser, string? name = null)
    {
        var response = await PostAsync(organiser, "/api/events", EventPayload(name));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<EventResponse>(response);
    }

    protected async Task<CategoryResponse> CreateCategoryAsync(
        HttpClient organiser, int eventId, string name = "Senior", int maxParticipants = 100)
    {
        var response = await PostAsync(organiser, $"/api/events/{eventId}/categories", new
        {
            categoryName = name,
            distanceKM = 10,
            maxParticipants,
            entryFee = 120.50
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<CategoryResponse>(response);
    }

    protected async Task<EnrolmentResponse> EnrolAsync(HttpClient participant, int categoryId)
    {
        var response = await PostAsync(participant, $"/api/categories/{categoryId}/enrolments");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<EnrolmentResponse>(response);
    }

    protected static Task<HttpResponseMessage> CaptureResultAsync(
        HttpClient organiser, int enrolmentId, string finishTime, int position, string status = "Finished") =>
        PostAsync(organiser, $"/api/enrolments/{enrolmentId}/result", new { finishTime, position, status });
}
