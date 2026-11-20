using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>Participants enrolling in categories, and Organisers capturing results.</summary>
public class EnrolmentAndResultTests : ApiTestBase, IClassFixture<RaceDayFactory>
{
    public EnrolmentAndResultTests(RaceDayFactory factory) : base(factory)
    {
    }

    // ---------- Enrolments ----------

    [Fact]
    public async Task Participant_CanEnrol_AndTheLinkIsCorrectlyRecorded()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, participantUser) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser, "Cape Town Cycle Tour");
        var category = await CreateCategoryAsync(organiser, ev.EventId);

        var response = await PostAsync(participant, $"/api/categories/{category.CategoryId}/enrolments");

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var enrolment = await ReadAsync<EnrolmentResponse>(response);
        Assert.Equal(participantUser.UserId, enrolment.ParticipantId);
        Assert.Equal(category.CategoryId, enrolment.CategoryId);
        Assert.Equal(ev.EventId, enrolment.EventId);
        Assert.Equal(EnrolmentStatus.Confirmed, enrolment.Status);
        Assert.StartsWith("CTC-", enrolment.BibNumber);
        Assert.True(enrolment.BibNumber!.Length <= 10); // fits Enrolments.BibNumber VARCHAR(10)

        // Verify what was persisted: participant + category (and so the event).
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var stored = await db.Enrolments.AsNoTracking()
            .Include(e => e.Category)
            .SingleAsync(e => e.EnrolmentId == enrolment.EnrolmentId);
        Assert.Equal(participantUser.UserId, stored.ParticipantId);
        Assert.Equal(category.CategoryId, stored.CategoryId);
        Assert.Equal(ev.EventId, stored.Category.EventId);
        Assert.Equal(enrolment.BibNumber, stored.BibNumber);
    }

    [Fact]
    public async Task Bib_NumbersAreUniquePerEnrolment()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (first, _) = await CreateParticipantAsync();
        var (second, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);

        var a = await EnrolAsync(first, category.CategoryId);
        var b = await EnrolAsync(second, category.CategoryId);

        Assert.NotEqual(a.BibNumber, b.BibNumber);
    }

    [Fact]
    public async Task EnrolledCategory_AppearsInTheParticipantsOwnEnrolments()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        await EnrolAsync(participant, category.CategoryId);

        var mine = await ReadAsync<List<EnrolmentResponse>>(await participant.GetAsync("/api/enrolments/me"));

        var single = Assert.Single(mine);
        Assert.Equal(ev.EventId, single.EventId);
        Assert.Equal(category.CategoryName, single.CategoryName);
    }

    [Fact]
    public async Task Organiser_CanViewAllEnrolmentsForTheirEvent()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (firstParticipant, first) = await CreateParticipantAsync();
        var (secondParticipant, second) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var tenK = await CreateCategoryAsync(organiser, ev.EventId, "10km");
        var twentyOneK = await CreateCategoryAsync(organiser, ev.EventId, "21km");
        await EnrolAsync(firstParticipant, tenK.CategoryId);
        await EnrolAsync(secondParticipant, twentyOneK.CategoryId);

        var response = await organiser.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var enrolments = await ReadAsync<List<EnrolmentResponse>>(response);
        Assert.Equal(2, enrolments.Count);
        Assert.Contains(enrolments, e => e.ParticipantId == first.UserId && e.CategoryName == "10km");
        Assert.Contains(enrolments, e => e.ParticipantId == second.UserId && e.CategoryName == "21km");
    }

    [Fact]
    public async Task Organiser_CannotViewEnrolmentsForAnotherOrganisersEvent()
    {
        var (owner, _) = await CreateOrganiserAsync();
        var (intruder, _) = await CreateOrganiserAsync();
        var ev = await CreateEventAsync(owner);

        var response = await intruder.GetAsync($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_Twice_InTheSameCategory_ReturnsConflict()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        await EnrolAsync(participant, category.CategoryId);

        var again = await PostAsync(participant, $"/api/categories/{category.CategoryId}/enrolments");

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Participant_CanEnrolInTwoCategoriesOfTheSameEvent()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var tenK = await CreateCategoryAsync(organiser, ev.EventId, "10km");
        var twentyOneK = await CreateCategoryAsync(organiser, ev.EventId, "21km");

        await EnrolAsync(participant, tenK.CategoryId);
        await EnrolAsync(participant, twentyOneK.CategoryId);

        var mine = await ReadAsync<List<EnrolmentResponse>>(await participant.GetAsync("/api/enrolments/me"));
        Assert.Equal(2, mine.Count);
    }

    [Fact]
    public async Task Enrol_InAFullCategory_ReturnsConflict()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (first, _) = await CreateParticipantAsync();
        var (second, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId, "Tiny", maxParticipants: 1);
        await EnrolAsync(first, category.CategoryId);

        var response = await PostAsync(second, $"/api/categories/{category.CategoryId}/enrolments");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_InACategoryThatDoesNotExist_ReturnsNotFound()
    {
        var (participant, _) = await CreateParticipantAsync();

        var response = await PostAsync(participant, "/api/categories/999999/enrolments");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enrol_AfterTheEventHasTakenPlace_ReturnsConflict()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);

        // Move the event into the past directly in the database (the API will not allow creating past events).
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
            var stored = await db.Events.SingleAsync(e => e.EventId == ev.EventId);
            stored.EventDate = DateOnly.FromDateTime(DateTime.Now.AddDays(-3));
            await db.SaveChangesAsync();
        }

        var response = await PostAsync(participant, $"/api/categories/{category.CategoryId}/enrolments");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Participant_CanCancelTheirEnrolment_AndThePlaceIsReleased()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (first, _) = await CreateParticipantAsync();
        var (second, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId, "Tiny", maxParticipants: 1);
        var enrolment = await EnrolAsync(first, category.CategoryId);

        var cancel = await first.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        var mine = await ReadAsync<List<EnrolmentResponse>>(await first.GetAsync("/api/enrolments/me"));
        Assert.Equal(EnrolmentStatus.Cancelled, Assert.Single(mine).Status);

        // The released place can be taken by someone else.
        Assert.Equal(HttpStatusCode.Created,
            (await PostAsync(second, $"/api/categories/{category.CategoryId}/enrolments")).StatusCode);
    }

    [Fact]
    public async Task Participant_CanReEnrolAfterCancelling_AndKeepsTheSameBibNumber()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var original = await EnrolAsync(participant, category.CategoryId);
        await participant.DeleteAsync($"/api/enrolments/{original.EnrolmentId}");

        var again = await EnrolAsync(participant, category.CategoryId);

        Assert.Equal(original.EnrolmentId, again.EnrolmentId);
        Assert.Equal(original.BibNumber, again.BibNumber);
        Assert.Equal(EnrolmentStatus.Confirmed, again.Status);
    }

    [Fact]
    public async Task Participant_CannotCancelSomeoneElsesEnrolment()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (owner, _) = await CreateParticipantAsync();
        var (intruder, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await EnrolAsync(owner, category.CategoryId);

        var cancel = await intruder.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.Forbidden, cancel.StatusCode);
    }

    [Fact]
    public async Task Cancel_AnEnrolmentThatDoesNotExist_ReturnsNotFound()
    {
        var (participant, _) = await CreateParticipantAsync();

        var cancel = await participant.DeleteAsync("/api/enrolments/999999");

        Assert.Equal(HttpStatusCode.NotFound, cancel.StatusCode);
    }

    // ---------- Results ----------

    [Fact]
    public async Task Organiser_CanCaptureAResult_AndTheParticipantCanViewIt()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, participantUser) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await EnrolAsync(participant, category.CategoryId);

        var capture = await CaptureResultAsync(organiser, enrolment.EnrolmentId, "00:48:30", 1);

        Assert.Equal(HttpStatusCode.Created, capture.StatusCode);
        var created = await ReadAsync<ResultResponse>(capture);
        Assert.Equal(TimeSpan.FromMinutes(48.5), created.FinishTime);
        Assert.Equal(1, created.Position);
        Assert.Equal(ResultStatus.Finished, created.Status);
        Assert.Equal(participantUser.UserId, created.ParticipantId);

        var mine = await ReadAsync<List<ResultResponse>>(await participant.GetAsync("/api/results/me"));
        var result = Assert.Single(mine);
        Assert.Equal(created.ResultId, result.ResultId);
        Assert.Equal(ev.EventName, result.EventName);
        Assert.Equal(category.CategoryName, result.CategoryName);
    }

    [Fact]
    public async Task Participant_SeesOnlyTheirOwnResults()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (firstParticipant, first) = await CreateParticipantAsync();
        var (secondParticipant, second) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var firstEnrolment = await EnrolAsync(firstParticipant, category.CategoryId);
        var secondEnrolment = await EnrolAsync(secondParticipant, category.CategoryId);

        await CaptureResultAsync(organiser, firstEnrolment.EnrolmentId, "00:40:00", 1);
        await CaptureResultAsync(organiser, secondEnrolment.EnrolmentId, "00:45:00", 2);

        var firstResults = await ReadAsync<List<ResultResponse>>(await firstParticipant.GetAsync("/api/results/me"));
        var secondResults = await ReadAsync<List<ResultResponse>>(await secondParticipant.GetAsync("/api/results/me"));

        Assert.Equal(first.UserId, Assert.Single(firstResults).ParticipantId);
        Assert.Equal(second.UserId, Assert.Single(secondResults).ParticipantId);
    }

    [Fact]
    public async Task EventLeaderboard_IsPublic_AndOrderedByPosition()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (firstParticipant, _) = await CreateParticipantAsync();
        var (secondParticipant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var firstEnrolment = await EnrolAsync(firstParticipant, category.CategoryId);
        var secondEnrolment = await EnrolAsync(secondParticipant, category.CategoryId);

        await CaptureResultAsync(organiser, firstEnrolment.EnrolmentId, "01:10:00", 2);
        await CaptureResultAsync(organiser, secondEnrolment.EnrolmentId, "01:05:00", 1);

        var response = await NewClient().GetAsync($"/api/events/{ev.EventId}/results"); // no login needed

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = await ReadAsync<List<ResultResponse>>(response);
        Assert.Equal(new int?[] { 1, 2 }, results.Select(r => r.Position).ToArray());
    }

    [Fact]
    public async Task EventLeaderboard_ForAnEventThatDoesNotExist_ReturnsNotFound()
    {
        var response = await NewClient().GetAsync("/api/events/999999/results");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_Twice_ForTheSameEnrolment_ReturnsConflict()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await EnrolAsync(participant, category.CategoryId);

        var first = await CaptureResultAsync(organiser, enrolment.EnrolmentId, "00:30:00", 1);
        var second = await CaptureResultAsync(organiser, enrolment.EnrolmentId, "00:31:00", 2);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_WithAPositionAlreadyTakenInTheCategory_ReturnsConflict()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (firstParticipant, _) = await CreateParticipantAsync();
        var (secondParticipant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var firstEnrolment = await EnrolAsync(firstParticipant, category.CategoryId);
        var secondEnrolment = await EnrolAsync(secondParticipant, category.CategoryId);

        await CaptureResultAsync(organiser, firstEnrolment.EnrolmentId, "00:30:00", 1);
        var clash = await CaptureResultAsync(organiser, secondEnrolment.EnrolmentId, "00:31:00", 1);

        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);
    }

    [Theory]
    [InlineData("00:00:00", 1, "Finished")]   // zero time
    [InlineData("24:00:00", 1, "Finished")]   // a day or more
    [InlineData("00:30:00", 0, "Finished")]   // position must be at least 1
    [InlineData("00:30:00", 1, "Disqualified")] // unknown status
    public async Task CaptureResult_WithInvalidValues_ReturnsBadRequest(string finishTime, int position, string status)
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await EnrolAsync(participant, category.CategoryId);

        var response = await CaptureResultAsync(organiser, enrolment.EnrolmentId, finishTime, position, status);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_FinishedWithoutTimeOrPosition_ReturnsBadRequest()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await EnrolAsync(participant, category.CategoryId);

        var response = await PostAsync(organiser, $"/api/enrolments/{enrolment.EnrolmentId}/result", new { status = "Finished" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_DidNotFinish_NeedsNoTimeOrPosition()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await EnrolAsync(participant, category.CategoryId);

        var response = await PostAsync(organiser, $"/api/enrolments/{enrolment.EnrolmentId}/result", new { status = "DNF" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await ReadAsync<ResultResponse>(response);
        Assert.Equal(ResultStatus.DNF, result.Status);
        Assert.Null(result.FinishTime);
        Assert.Null(result.Position);
    }

    [Fact]
    public async Task CaptureResult_ForAnEnrolmentThatDoesNotExist_ReturnsNotFound()
    {
        var (organiser, _) = await CreateOrganiserAsync();

        var response = await CaptureResultAsync(organiser, 999999, "00:30:00", 1);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_ForAnotherOrganisersEvent_IsForbidden()
    {
        var (owner, _) = await CreateOrganiserAsync();
        var (intruder, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(owner);
        var category = await CreateCategoryAsync(owner, ev.EventId);
        var enrolment = await EnrolAsync(participant, category.CategoryId);

        var response = await CaptureResultAsync(intruder, enrolment.EnrolmentId, "00:30:00", 1);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CaptureResult_ForACancelledEnrolment_ReturnsConflict()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await EnrolAsync(participant, category.CategoryId);
        await participant.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        var response = await CaptureResultAsync(organiser, enrolment.EnrolmentId, "00:30:00", 1);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanCorrectARecordedResult()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await EnrolAsync(participant, category.CategoryId);
        var created = await ReadAsync<ResultResponse>(await CaptureResultAsync(organiser, enrolment.EnrolmentId, "00:30:00", 3));

        var update = await PutAsync(organiser, $"/api/results/{created.ResultId}", new
        {
            finishTime = "00:29:15",
            position = 2,
            status = "Finished"
        });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await ReadAsync<ResultResponse>(update);
        Assert.Equal(new TimeSpan(0, 29, 15), updated.FinishTime);
        Assert.Equal(2, updated.Position);
    }

    [Fact]
    public async Task UpdateResult_ThatDoesNotExist_ReturnsNotFound()
    {
        var (organiser, _) = await CreateOrganiserAsync();

        var response = await PutAsync(organiser, "/api/results/999999", new { finishTime = "00:30:00", position = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotCorrectAnotherOrganisersResult()
    {
        var (owner, _) = await CreateOrganiserAsync();
        var (intruder, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(owner);
        var category = await CreateCategoryAsync(owner, ev.EventId);
        var enrolment = await EnrolAsync(participant, category.CategoryId);
        var created = await ReadAsync<ResultResponse>(await CaptureResultAsync(owner, enrolment.EnrolmentId, "00:30:00", 1));

        var update = await PutAsync(intruder, $"/api/results/{created.ResultId}", new { finishTime = "00:10:00", position = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, update.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCancel_OnceAResultIsRecorded()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var (participant, _) = await CreateParticipantAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var enrolment = await EnrolAsync(participant, category.CategoryId);
        await CaptureResultAsync(organiser, enrolment.EnrolmentId, "00:30:00", 1);

        var cancel = await participant.DeleteAsync($"/api/enrolments/{enrolment.EnrolmentId}");

        Assert.Equal(HttpStatusCode.Conflict, cancel.StatusCode);
    }
}
