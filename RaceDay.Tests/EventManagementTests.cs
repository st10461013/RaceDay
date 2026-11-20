using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>Organisers managing events, routes and categories; everyone browsing them; weather.</summary>
public class EventManagementTests : ApiTestBase, IClassFixture<RaceDayFactory>
{
    public EventManagementTests(RaceDayFactory factory) : base(factory)
    {
    }

    // ---------- Events: create ----------

    public async Task Organiser_CanCreateEvent()
    {
        var (organiser, user) = await CreateOrganiserAsync();

        var response = await PostAsync(organiser, "/api/events", new
        {
            eventName = "Table Mountain Challenge",
            eventDate = DateOnly.FromDateTime(DateTime.Now.AddDays(60)),
            location = "Cape Town, Western Cape",
            description = "A tough cycle up and around the mountain.",
            weatherLocationCode = "ZA-CT",
            eventType = EventType.Cycle,
            distanceKM = 42.2
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);

        var created = await ReadAsync<EventResponse>(response);
        Assert.Equal("Table Mountain Challenge", created.EventName);
        Assert.Equal(EventType.Cycle, created.EventType);
        Assert.Equal(42.2m, created.DistanceKM);
        Assert.Equal("ZA-CT", created.WeatherLocationCode);
        Assert.Equal(user.UserId, created.OrganiserId);
    }

    public async Task CreateEvent_WithMissingFields_ReturnsBadRequest()
    {
        var (organiser, _) = await CreateOrganiserAsync();

        var response = await PostAsync(organiser, "/api/events", new { eventName = "Incomplete" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateEvent_WithPastDate_ReturnsBadRequest()
    {
        var (organiser, _) = await CreateOrganiserAsync();

        var response = await PostAsync(organiser, "/api/events", EventPayload(daysAhead: -2));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public async Task CreateEvent_WithInvalidEventType_ReturnsBadRequest()
    {
        var (organiser, _) = await CreateOrganiserAsync();

        var response = await PostAsync(organiser, "/api/events", new
        {
            eventName = "Swim Gala",
            eventDate = DateOnly.FromDateTime(DateTime.Now.AddDays(10)),
            location = "Cape Town",
            eventType = "Swim",
            distanceKM = 1
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Events: browse ----------

    public async Task BothRoles_CanViewEvents()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var created = await CreateEventAsync(organiser);

        foreach (var client in new[] { organiser, participant })
        {
            var list = await client.GetAsync("/api/events");
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            Assert.Contains(await ReadAsync<List<EventResponse>>(list), e => e.EventId == created.EventId);

            var single = await client.GetAsync($"/api/events/{created.EventId}");
            Assert.Equal(HttpStatusCode.OK, single.StatusCode);
            Assert.Equal(created.EventName, (await ReadAsync<EventResponse>(single)).EventName);
        }
    }

    public async Task GetEvent_ThatDoesNotExist_ReturnsNotFound()
    {
        var response = await NewClient().GetAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetEvents_CanFilterByLocation_Date_Type_AndOwnership()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (otherOrganiser, _) = await CreateOrganiserAsync();
        var mine = await CreateEventAsync(organiser);
        var theirs = await CreateEventAsync(otherOrganiser);

        var onlyMine = await ReadAsync<List<EventResponse>>(await organiser.GetAsync("/api/events?mine=true"));
        Assert.Contains(onlyMine, e => e.EventId == mine.EventId);
        Assert.DoesNotContain(onlyMine, e => e.EventId == theirs.EventId);

        var byLocation = await ReadAsync<List<EventResponse>>(await NewClient().GetAsync("/api/events?location=Cape%20Town"));
        Assert.Contains(byLocation, e => e.EventId == mine.EventId);

        var elsewhere = await ReadAsync<List<EventResponse>>(await NewClient().GetAsync("/api/events?location=Nowhereville"));
        Assert.DoesNotContain(elsewhere, e => e.EventId == mine.EventId);

        var byDate = await ReadAsync<List<EventResponse>>(
            await NewClient().GetAsync($"/api/events?date={mine.EventDate:yyyy-MM-dd}"));
        Assert.Contains(byDate, e => e.EventId == mine.EventId);

        var walks = await ReadAsync<List<EventResponse>>(await NewClient().GetAsync("/api/events?type=Walk"));
        Assert.DoesNotContain(walks, e => e.EventId == mine.EventId); // the helper creates Run events
    }

    [Fact]
    public async Task GetEvents_ListsOnlyUpcomingEvents_UnlessIncludePastIsSet()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var upcoming = await CreateEventAsync(organiser);

        var list = await ReadAsync<List<EventResponse>>(await NewClient().GetAsync("/api/events"));

        Assert.Contains(list, e => e.EventId == upcoming.EventId);
        Assert.All(list, e => Assert.True(e.EventDate >= DateOnly.FromDateTime(DateTime.Now)));
    }

    [Fact]
    public async Task GetEvents_MineWithoutLogin_ReturnsUnauthorized()
    {
        var response = await NewClient().GetAsync("/api/events?mine=true");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ---------- Events: update ----------

    public async Task Organiser_CanUpdateTheirOwnEvent()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var created = await CreateEventAsync(organiser);

        var response = await PutAsync(organiser, $"/api/events/{created.EventId}", new
        {
            eventName = "Renamed Race",
            eventDate = DateOnly.FromDateTime(DateTime.Now.AddDays(45)),
            location = "Stellenbosch, Western Cape",
            description = "Now with a new description.",
            weatherLocationCode = "ZA-CT",
            eventType = EventType.Walk,
            distanceKM = 21.1
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await ReadAsync<EventResponse>(response);
        Assert.Equal("Renamed Race", updated.EventName);
        Assert.Equal("Stellenbosch, Western Cape", updated.Location);
        Assert.Equal(EventType.Walk, updated.EventType);

        var fetched = await ReadAsync<EventResponse>(await organiser.GetAsync($"/api/events/{created.EventId}"));
        Assert.Equal("Renamed Race", fetched.EventName);
    }

    public async Task UpdateEvent_ThatDoesNotExist_ReturnsNotFound()
    {
        var (organiser, _) = await CreateOrganiserAsync();

        var response = await PutAsync(organiser, "/api/events/999999", EventPayload());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public async Task Organiser_CannotUpdateAnotherOrganisersEvent()
    {
        var (owner, _) = await CreateOrganiserAsync();
        var (intruder, _) = await CreateOrganiserAsync();
        var created = await CreateEventAsync(owner);

        var response = await PutAsync(intruder, $"/api/events/{created.EventId}", EventPayload("Hijacked"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var unchanged = await ReadAsync<EventResponse>(await owner.GetAsync($"/api/events/{created.EventId}"));
        Assert.Equal(created.EventName, unchanged.EventName);
    }

    // ---------- Events: delete ----------

    [Fact]
    public async Task Organiser_CanDeleteTheirOwnEvent()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var created = await CreateEventAsync(organiser);
        await CreateCategoryAsync(organiser, created.EventId);

        var delete = await organiser.DeleteAsync($"/api/events/{created.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await organiser.GetAsync($"/api/events/{created.EventId}")).StatusCode);
    }

    public async Task DeleteEvent_RemovesItsRoutes_Categories_EnrolmentsAndResults()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        await PostAsync(organiser, $"/api/events/{ev.EventId}/routes", new { routeName = "Loop", distanceKM = 10 });
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await EnrolAsync(participant, category.CategoryId);
        Assert.Equal(HttpStatusCode.Created, (await CaptureResultAsync(organiser, enrolment.EnrolmentId, "00:50:00", 1)).StatusCode);

        var delete = await organiser.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        var myEnrolments = await ReadAsync<List<EnrolmentResponse>>(await participant.GetAsync("/api/enrolments/me"));
        Assert.DoesNotContain(myEnrolments, e => e.EventId == ev.EventId);
        var myResults = await ReadAsync<List<ResultResponse>>(await participant.GetAsync("/api/results/me"));
        Assert.DoesNotContain(myResults, r => r.EventId == ev.EventId);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent()
    {
        var (owner, _) = await CreateOrganiserAsync();
        var (intruder, _) = await CreateOrganiserAsync();
        var created = await CreateEventAsync(owner);

        var delete = await intruder.DeleteAsync($"/api/events/{created.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/api/events/{created.EventId}")).StatusCode);
    }

    public async Task DeleteEvent_ThatDoesNotExist_ReturnsNotFound()
    {
        var (organiser, _) = await CreateOrganiserAsync();

        var delete = await organiser.DeleteAsync("/api/events/999999");

        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    // ---------- Categories ----------

    public async Task Organiser_CanDefineCategories_AndAnyoneCanViewThem()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var ev = await CreateEventAsync(organiser);

        var under20 = await PostAsync(organiser, $"/api/events/{ev.EventId}/categories", new
        {
            categoryName = "Under 20",
            distanceKM = 10,
            maxParticipants = 250,
            entryFee = 90
        });
        var tenK = await PostAsync(organiser, $"/api/events/{ev.EventId}/categories", new
        {
            categoryName = "21km",
            distanceKM = 21.1
        });

        Assert.Equal(HttpStatusCode.Created, under20.StatusCode);
        Assert.Equal(HttpStatusCode.Created, tenK.StatusCode);

        var list = await ReadAsync<List<CategoryResponse>>(await NewClient().GetAsync($"/api/events/{ev.EventId}/categories"));
        Assert.Contains(list, c => c.CategoryName == "Under 20" && c.MaxParticipants == 250 && c.EntryFee == 90m);
        Assert.Contains(list, c => c.CategoryName == "21km" && c.MaxParticipants == 100 && c.EntryFee == 0m); // defaults
    }

    [Fact]
    public async Task CreateCategory_WithInvalidValues_ReturnsBadRequest()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var url = $"/api/events/{ev.EventId}/categories";

        var noName = await PostAsync(organiser, url, new { distanceKM = 10 });
        var noDistance = await PostAsync(organiser, url, new { categoryName = "Far" });
        var negativeFee = await PostAsync(organiser, url, new { categoryName = "Cheap", distanceKM = 5, entryFee = -1 });

        Assert.Equal(HttpStatusCode.BadRequest, noName.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noDistance.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, negativeFee.StatusCode);
    }

    public async Task CreateCategory_WithDuplicateName_ReturnsConflict()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        await CreateCategoryAsync(organiser, ev.EventId, "Senior");

        var duplicate = await PostAsync(organiser, $"/api/events/{ev.EventId}/categories", new
        {
            categoryName = "senior",
            distanceKM = 10
        });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateAndDeleteACategory()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId, "Veteran");
        var url = $"/api/categories/{category.CategoryId}";

        var update = await PutAsync(organiser, url, new { categoryName = "Masters", distanceKM = 21.1, maxParticipants = 50, entryFee = 200 });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await ReadAsync<CategoryResponse>(update);
        Assert.Equal("Masters", updated.CategoryName);
        Assert.Equal(50, updated.MaxParticipants);

        Assert.Equal(HttpStatusCode.NoContent, (await organiser.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await organiser.GetAsync(url)).StatusCode);
    }

    public async Task DeleteCategory_WithEnrolments_ReturnsConflict()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        await EnrolAsync(participant, category.CategoryId);

        var delete = await organiser.DeleteAsync($"/api/categories/{category.CategoryId}");

        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }

    public async Task UpdateCategory_BelowCurrentEnrolments_ReturnsConflict()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (first, _) = await CreateParticipantAsync();
        var (second, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        await EnrolAsync(first, category.CategoryId);
        await EnrolAsync(second, category.CategoryId);

        var update = await PutAsync(organiser, $"/api/categories/{category.CategoryId}", new
        {
            categoryName = category.CategoryName,
            distanceKM = 10,
            maxParticipants = 1 // two participants are already enrolled
        });

        Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
    }

    public async Task Organiser_CannotManageCategoriesOfAnotherOrganisersEvent()
    {
        var (owner, _) = await CreateOrganiserAsync();
        var (intruder, _) = await CreateOrganiserAsync();
        var ev = await CreateEventAsync(owner);
        var category = await CreateCategoryAsync(owner, ev.EventId);

        var create = await PostAsync(intruder, $"/api/events/{ev.EventId}/categories", new
        {
            categoryName = "Sneaky",
            distanceKM = 5
        });
        var update = await PutAsync(intruder, $"/api/categories/{category.CategoryId}", new
        {
            categoryName = "Renamed",
            distanceKM = 5
        });
        var delete = await intruder.DeleteAsync($"/api/categories/{category.CategoryId}");

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    // ---------- Routes ----------

    public async Task Organiser_CanAddUpdateAndDeleteRoutes_AndAnyoneCanListThem()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var ev = await CreateEventAsync(organiser);

        var create = await PostAsync(organiser, $"/api/events/{ev.EventId}/routes", new
        {
            routeName = "Peninsula Loop",
            distanceKM = 109,
            elevationGainM = 1200,
            mapURL = "https://maps.example.com/loop"
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var route = await ReadAsync<RouteResponse>(create);
        Assert.Equal(1200, route.ElevationGainM);

        var list = await ReadAsync<List<RouteResponse>>(await NewClient().GetAsync($"/api/events/{ev.EventId}/routes"));
        Assert.Contains(list, r => r.RouteId == route.RouteId);

        var update = await PutAsync(organiser, $"/api/routes/{route.RouteId}", new { routeName = "Shorter Loop", distanceKM = 35 });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await ReadAsync<RouteResponse>(update);
        Assert.Equal("Shorter Loop", updated.RouteName);
        Assert.Equal(0, updated.ElevationGainM); // default

        Assert.Equal(HttpStatusCode.NoContent, (await organiser.DeleteAsync($"/api/routes/{route.RouteId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await organiser.GetAsync($"/api/routes/{route.RouteId}")).StatusCode);
    }

    public async Task CreateRoute_WithInvalidValues_ReturnsBadRequest()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var url = $"/api/events/{ev.EventId}/routes";

        var noDistance = await PostAsync(organiser, url, new { routeName = "Loop" });
        var badUrl = await PostAsync(organiser, url, new { routeName = "Loop", distanceKM = 5, mapURL = "not a url" });

        Assert.Equal(HttpStatusCode.BadRequest, noDistance.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, badUrl.StatusCode);
    }

    public async Task Organiser_CannotManageRoutesOfAnotherOrganisersEvent()
    {
        var (owner, _) = await CreateOrganiserAsync();
        var (intruder, _) = await CreateOrganiserAsync();
        var ev = await CreateEventAsync(owner);
        var route = await ReadAsync<RouteResponse>(
            await PostAsync(owner, $"/api/events/{ev.EventId}/routes", new { routeName = "Loop", distanceKM = 10 }));

        var create = await PostAsync(intruder, $"/api/events/{ev.EventId}/routes", new { routeName = "Sneaky", distanceKM = 1 });
        var update = await PutAsync(intruder, $"/api/routes/{route.RouteId}", new { routeName = "Sneaky", distanceKM = 1 });
        var delete = await intruder.DeleteAsync($"/api/routes/{route.RouteId}");

        Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    // ---------- Weather ----------

    public async Task BothRoles_CanViewTheWeatherForAnEvent()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);

        foreach (var client in new[] { organiser, participant })
        {
            var response = await client.GetAsync($"/api/events/{ev.EventId}/weather");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var weather = await ReadAsync<WeatherResponse>(response);
            Assert.Equal("Cape Town, Western Cape", weather.Location);
            Assert.Equal(ev.EventDate, weather.ForecastDate);
            Assert.Equal(21.5, weather.Temperature);
            Assert.Equal("Partly cloudy", weather.Conditions);
            Assert.Equal(14.2, weather.WindSpeed);
        }
    }

    public async Task Weather_ForAnEventWithoutALocationCode_ReturnsNotFound()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var created = await ReadAsync<EventResponse>(
            await PostAsync(organiser, "/api/events", EventPayload(weatherCode: null)));

        var response = await organiser.GetAsync($"/api/events/{created.EventId}/weather");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    public async Task Weather_ForAnUnknownLocationCode_ReturnsNotFound()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var created = await ReadAsync<EventResponse>(
            await PostAsync(organiser, "/api/events", EventPayload(weatherCode: "ZA-NOPE")));

        var response = await organiser.GetAsync($"/api/events/{created.EventId}/weather");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Weather_WhenTheProviderIsDown_ReturnsServiceUnavailable()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var created = await ReadAsync<EventResponse>(
            await PostAsync(organiser, "/api/events", EventPayload(weatherCode: "ZA-DOWN")));

        var response = await organiser.GetAsync($"/api/events/{created.EventId}/weather");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    public async Task Weather_ForAnEventThatDoesNotExist_ReturnsNotFound()
    {
        var (participant, _) = await CreateParticipantAsync();

        var response = await participant.GetAsync("/api/events/999999/weather");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
