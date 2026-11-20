namespace RaceDay.Api.Models;

// Entities follow the Part 1 ERD (docs/ERD.png) and docs/schema.sql:
// Users, Events, Routes, Categories, Enrolments, Results.

/// <summary>Users table: holds both roles via the Role column.</summary>
public class AppUser
{
    public int UserId { get; set; }
    public UserRole Role { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    /// <summary>PBKDF2 hash of the password. The plain-text password is never stored.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }
    public string? ProfileImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<Event> OrganisedEvents { get; set; } = new List<Event>();
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

/// <summary>Events table: created by an Organiser.</summary>
public class Event
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? BannerImageUrl { get; set; }
    public string? WeatherLocationCode { get; set; }

    /// <summary>Part 2 addition: Run, Walk or Cycle (required by the Part 2 brief).</summary>
    public EventType EventType { get; set; } = EventType.Run;

    /// <summary>Part 2 addition: overall event distance (required by the Part 2 brief).</summary>
    public decimal DistanceKM { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public AppUser Organiser { get; set; } = null!;
    public ICollection<RaceRoute> Routes { get; set; } = new List<RaceRoute>();
    public ICollection<Category> Categories { get; set; } = new List<Category>();
}

/// <summary>Routes table: the course details of an event.</summary>
public class RaceRoute
{
    public int RouteId { get; set; }
    public int EventId { get; set; }
    public string RouteName { get; set; } = string.Empty;
    public decimal DistanceKM { get; set; }
    public int ElevationGainM { get; set; }
    public string? MapURL { get; set; }

    public Event Event { get; set; } = null!;
}

/// <summary>Categories table: an individual race within an event (e.g. "10km Fun Run").</summary>
public class Category
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal DistanceKM { get; set; }
    public int MaxParticipants { get; set; } = 100;
    public decimal EntryFee { get; set; }

    public Event Event { get; set; } = null!;
    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

/// <summary>Enrolments table: a Participant entering a Category (and therefore its Event).</summary>
public class Enrolment
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public int CategoryId { get; set; }
    public DateTime EnrolmentDate { get; set; } = DateTime.Now;
    public string? BibNumber { get; set; }
    public EnrolmentStatus Status { get; set; } = EnrolmentStatus.Confirmed;

    public AppUser Participant { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public EventResult? Result { get; set; }
}

/// <summary>Results table: at most one result per enrolment.</summary>
public class EventResult
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public TimeSpan? FinishTime { get; set; }
    public int? Position { get; set; }
    public ResultStatus Status { get; set; } = ResultStatus.Finished;

    public Enrolment Enrolment { get; set; } = null!;
}
