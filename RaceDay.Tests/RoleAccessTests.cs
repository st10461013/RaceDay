using RaceDay.Api.Models;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>
/// Role-based access: Organiser functionality is closed to Participants and vice versa,
/// and anonymous callers are turned away from everything non-public.
/// </summary>
public class RoleAccessTests : ApiTestBase, IClassFixture<RaceDayFactory>
{
    public RoleAccessTests(RaceDayFactory factory) : base(factory)
    {
    }

    // ---------- Participant attempting Organiser functionality ----------

    public async Task Participant_CannotCreateUpdateOrDeleteEvents()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);

        var create = await PostAsync(participant, "/api/events", EventPayload());
        var update = await PutAsync(participant, $"/api/events/{ev.EventId}", EventPayload("Hijacked"));
        var delete = await participant.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    public async Task Participant_CannotManageCategoriesOrRoutes()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);

        var createCategory = await PostAsync(participant, $"/api/events/{ev.EventId}/categories", new
        {
            categoryName = "Mine",
            distanceKM = 5
        });
        var updateCategory = await PutAsync(participant, $"/api/categories/{category.CategoryId}", new
        {
            categoryName = "Renamed",
            distanceKM = 5
        });
        var deleteCategory = await participant.DeleteAsync($"/api/categories/{category.CategoryId}");
        var createRoute = await PostAsync(participant, $"/api/events/{ev.EventId}/routes", new
        {
            routeName = "Mine",
            distanceKM = 5
        });

        Assert.Equal(HttpStatusCode.Forbidden, createCategory.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, updateCategory.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleteCategory.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, createRoute.StatusCode);
    }

    public async Task Participant_CannotViewAnEventsEnrolments_OrManageResults()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await EnrolAsync(participant, category.CategoryId);

        var viewEnrolments = await participant.GetAsync($"/api/events/{ev.EventId}/enrolments");
        var capture = await CaptureResultAsync(participant, enrolment.EnrolmentId, "00:40:00", 1);
        var update = await PutAsync(participant, "/api/results/1", new { finishTime = "00:40:00", position = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, viewEnrolments.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, capture.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
    }

    public async Task Participant_CannotFilterEventsByOrganiserOwnership()
    {
        var (participant, _) = await CreateParticipantAsync();

        var response = await participant.GetAsync("/api/events?mine=true");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ---------- Organiser attempting Participant functionality ----------

    public async Task Organiser_CannotEnrolInACategory()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);

        var response = await PostAsync(organiser, $"/api/categories/{category.CategoryId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    public async Task Organiser_CannotUseParticipantOnlyEndpoints()
    {
        var (organiser, _) = await CreateOrganiserAsync();

        var myEnrolments = await organiser.GetAsync("/api/enrolments/me");
        var myResults = await organiser.GetAsync("/api/results/me");
        var cancel = await organiser.DeleteAsync("/api/enrolments/1");

        Assert.Equal(HttpStatusCode.Forbidden, myEnrolments.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, myResults.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
    }

    // ---------- Anonymous callers ----------

    public async Task Anonymous_CannotUseAnyOrganiserOrParticipantAction()
    {
        var client = NewClient();

        var responses = new[]
        {
            await PostAsync(client, "/api/events", EventPayload()),
            await PutAsync(client, "/api/events/1", EventPayload()),
            await client.DeleteAsync("/api/events/1"),
            await PostAsync(client, "/api/events/1/categories", new { categoryName = "x", distanceKM = 1 }),
            await PutAsync(client, "/api/categories/1", new { categoryName = "x", distanceKM = 1 }),
            await client.DeleteAsync("/api/categories/1"),
            await PostAsync(client, "/api/events/1/routes", new { routeName = "x", distanceKM = 1 }),
            await PutAsync(client, "/api/routes/1", new { routeName = "x", distanceKM = 1 }),
            await client.DeleteAsync("/api/routes/1"),
            await PostAsync(client, "/api/categories/1/enrolments"),
            await client.DeleteAsync("/api/enrolments/1"),
            await client.GetAsync("/api/events/1/enrolments"),
            await PostAsync(client, "/api/enrolments/1/result", new { finishTime = "00:30:00", position = 1 }),
            await PutAsync(client, "/api/results/1", new { finishTime = "00:30:00", position = 1 })
        };

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode));
    }

    // ---------- The role comes from the server-side session, not from the client ----------

    public async Task ClientCannotElevateItsRole_ByRegisteringAsParticipantThenCallingOrganiserEndpoints()
    {
        var client = NewClient();
        var email = UniqueEmail("sneaky");
        await RegisterAsync(client, email, UserRole.Participant);
        await LoginAsync(client, email);

        var response = await PostAsync(client, "/api/events", EventPayload());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    public async Task LoggingInAsADifferentUser_ReplacesTheSessionRole()
    {
        var client = NewClient();
        var participantEmail = UniqueEmail("p");
        var organiserEmail = UniqueEmail("o");
        await RegisterAsync(client, participantEmail, UserRole.Participant);
        await RegisterAsync(client, organiserEmail, UserRole.Organiser, RaceDayFactory.OrganiserCode);

        await LoginAsync(client, participantEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await PostAsync(client, "/api/events", EventPayload())).StatusCode);

        await LoginAsync(client, organiserEmail);
        Assert.Equal(HttpStatusCode.Created, (await PostAsync(client, "/api/events", EventPayload())).StatusCode);
    }
}
