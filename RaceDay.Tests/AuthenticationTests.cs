using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>User registration, login, session handling, profile and password storage.</summary>
public class AuthenticationTests : ApiTestBase, IClassFixture<RaceDayFactory>
{
    public AuthenticationTests(RaceDayFactory factory) : base(factory)
    {
    }

    // ---------- Registration: success ----------

    [Fact]
    public async Task Register_Participant_ReturnsCreated_AndNeverExposesThePassword()
    {
        var email = UniqueEmail("participant");

        var response = await RegisterAsync(NewClient(), email, UserRole.Participant);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(Password, body);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);

        var user = JsonSerializer.Deserialize<UserResponse>(body, Json.Options)!;
        Assert.Equal(email, user.Email);
        Assert.Equal(UserRole.Participant, user.Role);
        Assert.Equal("0821234567", user.PhoneNumber);
        Assert.True(user.UserId > 0);
    }

    [Fact]
    public async Task Register_Organiser_WithValidAccessCode_ReturnsCreated()
    {
        var response = await RegisterAsync(
            NewClient(), UniqueEmail("organiser"), UserRole.Organiser, RaceDayFactory.OrganiserCode);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(UserRole.Organiser, (await ReadAsync<UserResponse>(response)).Role);
    }

    // ---------- Registration: failure ----------

    [Fact]
    public async Task Register_Organiser_WithoutAccessCode_IsForbidden()
    {
        var response = await RegisterAsync(NewClient(), UniqueEmail("organiser"), UserRole.Organiser);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_Organiser_WithWrongAccessCode_IsForbidden()
    {
        var response = await RegisterAsync(NewClient(), UniqueEmail("organiser"), UserRole.Organiser, "not-the-code");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsConflict()
    {
        var email = UniqueEmail("duplicate");
        await RegisterAsync(NewClient(), email, UserRole.Participant);

        var second = await RegisterAsync(NewClient(), email.ToUpperInvariant(), UserRole.Participant);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Theory]
    [InlineData("short1")]          // too short
    [InlineData("onlyletters")]     // no digit
    [InlineData("12345678")]        // no letter
    public async Task Register_WeakPassword_ReturnsBadRequest(string password)
    {
        var response = await RegisterAsync(NewClient(), UniqueEmail("weak"), UserRole.Participant, password: password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidEmail_ReturnsBadRequest()
    {
        var response = await RegisterAsync(NewClient(), "not-an-email", UserRole.Participant);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_MissingRole_ReturnsBadRequest()
    {
        var response = await PostAsync(NewClient(), "/api/auth/register", new
        {
            firstName = "No",
            lastName = "Role",
            email = UniqueEmail("norole"),
            password = Password
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_UnknownRoleValue_ReturnsBadRequest()
    {
        var response = await PostAsync(NewClient(), "/api/auth/register", new
        {
            role = 99,
            firstName = "Bad",
            lastName = "Role",
            email = UniqueEmail("badrole"),
            password = Password
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Login ----------

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsUserAndStartsASession()
    {
        var client = NewClient();
        var email = UniqueEmail("login");
        await RegisterAsync(client, email, UserRole.Participant);

        var login = await LoginAsync(client, email);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(UserRole.Participant, (await ReadAsync<UserResponse>(login)).Role);

        // The session cookie now authenticates later requests.
        var me = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.Equal(email, (await ReadAsync<UserResponse>(me)).Email);
    }

    [Fact]
    public async Task Login_IsCaseInsensitiveOnEmail()
    {
        var client = NewClient();
        var email = UniqueEmail("case");
        await RegisterAsync(client, email, UserRole.Participant);

        var login = await LoginAsync(client, email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized_AndNoSession()
    {
        var client = NewClient();
        var email = UniqueEmail("wrongpass");
        await RegisterAsync(client, email, UserRole.Participant);

        var login = await LoginAsync(client, email, "Wrong-Passw0rd");

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsUnauthorized()
    {
        var login = await LoginAsync(NewClient(), UniqueEmail("ghost"));

        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Login_WithMissingFields_ReturnsBadRequest()
    {
        var response = await PostAsync(NewClient(), "/api/auth/login", new { email = "someone@example.com" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---------- Sessions: non-public areas need a login, public areas do not ----------

    [Theory]
    [InlineData("/api/users/me")]
    [InlineData("/api/enrolments/me")]
    [InlineData("/api/results/me")]
    [InlineData("/api/events/1/enrolments")]
    [InlineData("/api/events/1/weather")]
    public async Task NonPublicEndpoints_WithoutLogin_ReturnUnauthorized(string url)
    {
        var response = await NewClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PublicEndpoints_WorkWithoutLogin()
    {
        var (organiser, _) = await CreateOrganiserAsync();
        var ev = await CreateEventAsync(organiser);
        var category = await CreateCategoryAsync(organiser, ev.EventId);
        var anonymous = NewClient();

        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/api/events")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/events/{ev.EventId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/events/{ev.EventId}/categories")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/categories/{category.CategoryId}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/events/{ev.EventId}/routes")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/events/{ev.EventId}/results")).StatusCode);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var (client, _) = await CreateParticipantAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users/me")).StatusCode);

        var logout = await PostAsync(client, "/api/auth/logout");

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_WithoutLogin_ReturnsUnauthorized()
    {
        var logout = await PostAsync(NewClient(), "/api/auth/logout");

        Assert.Equal(HttpStatusCode.Unauthorized, logout.StatusCode);
    }

    [Fact]
    public async Task Sessions_AreIsolatedBetweenClients()
    {
        var (_, _) = await CreateOrganiserAsync();
        var anonymous = NewClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/users/me")).StatusCode);
    }

    // ---------- Profile ----------

    [Fact]
    public async Task Profile_CanBeViewedAndUpdated_ByBothRoles()
    {
        var (participant, _) = await CreateParticipantAsync();
        var (organiser, _) = await CreateOrganiserAsync();

        foreach (var client in new[] { participant, organiser })
        {
            var current = await ReadAsync<UserResponse>(await client.GetAsync("/api/users/me"));

            var update = await PutAsync(client, "/api/users/me", new
            {
                firstName = "Updated",
                lastName = "Name",
                phoneNumber = "0731122334"
            });

            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            var updated = await ReadAsync<UserResponse>(update);
            Assert.Equal("Updated", updated.FirstName);
            Assert.Equal("0731122334", updated.PhoneNumber);
            Assert.Equal(current.Email, updated.Email);   // email cannot be changed
            Assert.Equal(current.Role, updated.Role);     // role cannot be changed
        }
    }

    [Fact]
    public async Task Profile_UpdateWithMissingName_ReturnsBadRequest()
    {
        var (client, _) = await CreateParticipantAsync();

        var update = await PutAsync(client, "/api/users/me", new { lastName = "OnlyLast" });

        Assert.Equal(HttpStatusCode.BadRequest, update.StatusCode);
    }

    // ---------- Password storage ----------

    [Fact]
    public async Task Password_IsStoredAsAHash_NotInItsOriginalForm()
    {
        var email = UniqueEmail("hashed");
        await RegisterAsync(NewClient(), email, UserRole.Participant);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        var stored = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email);

        Assert.NotEqual(Password, stored.PasswordHash);
        Assert.DoesNotContain(Password, stored.PasswordHash);
        Assert.StartsWith("PBKDF2$", stored.PasswordHash);
        Assert.True(stored.PasswordHash.Length <= 255); // fits Users.PasswordHash VARCHAR(255)
    }

    [Fact]
    public void PasswordHasher_VerifiesCorrectPassword_AndRejectsWrongOnes()
    {
        IPasswordHasher hasher = new Pbkdf2PasswordHasher();
        var hash = hasher.Hash("Passw0rd!");

        Assert.True(hasher.Verify("Passw0rd!", hash));
        Assert.False(hasher.Verify("passw0rd!", hash));
        Assert.False(hasher.Verify("", hash));
    }

    [Fact]
    public void PasswordHasher_UsesAUniqueSaltForEveryHash()
    {
        IPasswordHasher hasher = new Pbkdf2PasswordHasher();

        Assert.NotEqual(hasher.Hash("Passw0rd!"), hasher.Hash("Passw0rd!"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plain-text-password")]
    [InlineData("PBKDF2$abc$def$ghi")]
    [InlineData("PBKDF2$1000$not-base64!$also-not-base64!")]
    public void PasswordHasher_RejectsMalformedStoredHashes(string storedHash)
    {
        IPasswordHasher hasher = new Pbkdf2PasswordHasher();

        Assert.False(hasher.Verify("Passw0rd!", storedHash));
    }
}
