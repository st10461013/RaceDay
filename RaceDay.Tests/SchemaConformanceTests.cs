using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Tests.Infrastructure;

namespace RaceDay.Tests;

/// <summary>
/// Checks that the code-first model matches the Part 1 schema (docs/schema.sql): table names, column names,
/// column types, unique constraints and non-cascading foreign keys. Events additionally carries the two
/// Part 2 columns EventType and DistanceKM.
/// </summary>
public class SchemaConformanceTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _factory;

    public SchemaConformanceTests(RaceDayFactory factory)
    {
        _factory = factory;
    }

    private static string? Annotation(IAnnotatable item, string name) => item.FindAnnotation(name)?.Value as string;

    private IEntityType Entity<T>()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
        return db.Model.FindEntityType(typeof(T))!;
    }

    private static void AssertColumns(IEntityType entity, params (string Property, string Column, string? Type)[] expected)
    {
        foreach (var (property, column, type) in expected)
        {
            var p = entity.FindProperty(property);
            Assert.True(p is not null, $"{entity.ClrType.Name} has no property {property}");

            var actualColumn = Annotation(p!, "Relational:ColumnName") ?? p!.Name;
            Assert.Equal(column, actualColumn);

            if (type is not null)
            {
                Assert.Equal(type, Annotation(p!, "Relational:ColumnType"));
            }
        }
    }

    public void Users_MatchesPart1Schema()
    {
        var users = Entity<AppUser>();

        Assert.Equal("Users", Annotation(users, "Relational:TableName"));
        AssertColumns(users,
            (nameof(AppUser.UserId), "UserID", null),
            (nameof(AppUser.Role), "Role", "varchar(20)"),
            (nameof(AppUser.FirstName), "FirstName", "varchar(50)"),
            (nameof(AppUser.LastName), "LastName", "varchar(50)"),
            (nameof(AppUser.Email), "Email", "varchar(100)"),
            (nameof(AppUser.PasswordHash), "PasswordHash", "varchar(255)"),
            (nameof(AppUser.PhoneNumber), "PhoneNumber", "varchar(20)"),
            (nameof(AppUser.ProfileImageUrl), "ProfileImageUrl", "varchar(1000)"),
            (nameof(AppUser.CreatedAt), "CreatedAt", "datetime"));

        Assert.Contains(users.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "Email" }));
    }

    public void Events_MatchesPart1Schema_PlusTheTwoPart2Columns()
    {
        var events = Entity<Event>();

        Assert.Equal("Events", Annotation(events, "Relational:TableName"));
        AssertColumns(events,
            (nameof(Event.EventId), "EventID", null),
            (nameof(Event.OrganiserId), "OrganiserID", null),
            (nameof(Event.EventName), "EventName", "varchar(100)"),
            (nameof(Event.EventDate), "EventDate", "date"),
            (nameof(Event.Location), "Location", "varchar(150)"),
            (nameof(Event.Description), "Description", "varchar(8000)"),
            (nameof(Event.BannerImageUrl), "BannerImageUrl", "varchar(1000)"),
            (nameof(Event.WeatherLocationCode), "WeatherLocationCode", "varchar(20)"),
            (nameof(Event.CreatedAt), "CreatedAt", "datetime"),
            // Part 2 additions
            (nameof(Event.EventType), "EventType", "varchar(20)"),
            (nameof(Event.DistanceKM), "DistanceKM", "decimal(6,2)"));
    }

    public void Routes_MatchesPart1Schema()
    {
        var routes = Entity<RaceRoute>();

        Assert.Equal("Routes", Annotation(routes, "Relational:TableName"));
        AssertColumns(routes,
            (nameof(RaceRoute.RouteId), "RouteID", null),
            (nameof(RaceRoute.EventId), "EventID", null),
            (nameof(RaceRoute.RouteName), "RouteName", "varchar(100)"),
            (nameof(RaceRoute.DistanceKM), "DistanceKM", "decimal(6,2)"),
            (nameof(RaceRoute.ElevationGainM), "ElevationGainM", null),
            (nameof(RaceRoute.MapURL), "MapURL", "varchar(255)"));
    }

    [Fact]
    public void Categories_MatchesPart1Schema()
    {
        var categories = Entity<Category>();

        Assert.Equal("Categories", Annotation(categories, "Relational:TableName"));
        AssertColumns(categories,
            (nameof(Category.CategoryId), "CategoryID", null),
            (nameof(Category.EventId), "EventID", null),
            (nameof(Category.CategoryName), "CategoryName", "varchar(50)"),
            (nameof(Category.DistanceKM), "DistanceKM", "decimal(6,2)"),
            (nameof(Category.MaxParticipants), "MaxParticipants", null),
            (nameof(Category.EntryFee), "EntryFee", "decimal(8,2)"));
    }

    public void Enrolments_MatchesPart1Schema()
    {
        var enrolments = Entity<Enrolment>();

        Assert.Equal("Enrolments", Annotation(enrolments, "Relational:TableName"));
        AssertColumns(enrolments,
            (nameof(Enrolment.EnrolmentId), "EnrolmentID", null),
            (nameof(Enrolment.ParticipantId), "ParticipantID", null),
            (nameof(Enrolment.CategoryId), "CategoryID", null),
            (nameof(Enrolment.EnrolmentDate), "EnrolmentDate", "datetime"),
            (nameof(Enrolment.BibNumber), "BibNumber", "varchar(10)"),
            (nameof(Enrolment.Status), "Status", "varchar(20)"));

        // UQ_Enrolments_Participant_Category
        Assert.Contains(enrolments.GetIndexes(), i =>
            i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "ParticipantId", "CategoryId" }));
    }

    public void Results_MatchesPart1Schema()
    {
        var results = Entity<EventResult>();

        Assert.Equal("Results", Annotation(results, "Relational:TableName"));
        AssertColumns(results,
            (nameof(EventResult.ResultId), "ResultID", null),
            (nameof(EventResult.EnrolmentId), "EnrolmentID", null),
            (nameof(EventResult.FinishTime), "FinishTime", "time"),
            (nameof(EventResult.Position), "Position", null),
            (nameof(EventResult.Status), "Status", "varchar(20)"));

        // EnrolmentID is UNIQUE: at most one result per enrolment.
        Assert.Contains(results.GetIndexes(), i =>
            i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual(new[] { "EnrolmentId" }));
    }

    public void AllForeignKeys_AreNonCascading_AsInThePart1Script()
    {
        var entities = new[]
        {
            Entity<Event>(), Entity<RaceRoute>(), Entity<Category>(), Entity<Enrolment>(), Entity<EventResult>()
        };

        foreach (var entity in entities)
        {
            foreach (var foreignKey in entity.GetForeignKeys())
            {
                Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
            }
        }
    }
}
