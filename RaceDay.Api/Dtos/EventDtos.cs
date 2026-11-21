using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

/// <summary>
/// Data an Organiser supplies to create or update an event. The Part 1 plan lists eventName, eventDate,
/// location, description and weatherLocationCode; eventType and distanceKM are added because the Part 2 brief
/// requires every event to capture a distance and a type (run, walk or cycle).
/// </summary>
public class EventRequest
{
    [Required, StringLength(100)]
    public string EventName { get; set; } = string.Empty;

    /// <summary>Date of the event (yyyy-MM-dd). Must be today or later when creating.</summary>
    [Required]
    public DateOnly? EventDate { get; set; }

    [Required, StringLength(150)]
    public string Location { get; set; } = string.Empty;

    [StringLength(8000)]
    public string? Description { get; set; }

    /// <summary>Key used for the weather forecast, e.g. ZA-CT (Cape Town) or "lat,lon".</summary>
    [StringLength(20)]
    public string? WeatherLocationCode { get; set; }

    /// <summary>Run, Walk or Cycle.</summary>
    [Required]
    public EventType? EventType { get; set; }

    /// <summary>Overall distance in kilometres.</summary>
    [Required, Range(0.1, 9999)]
    public decimal? DistanceKM { get; set; }
}

public class EventResponse
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string OrganiserName { get; set; } = string.Empty;
    public string EventName { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? BannerImageUrl { get; set; }
    public string? WeatherLocationCode { get; set; }
    public EventType EventType { get; set; }
    public decimal DistanceKM { get; set; }
    public DateTime CreatedAt { get; set; }

    public static readonly Expression<Func<Event, EventResponse>> Projection = e => new EventResponse
    {
        EventId = e.EventId,
        OrganiserId = e.OrganiserId,
        OrganiserName = e.Organiser.FirstName + " " + e.Organiser.LastName,
        EventName = e.EventName,
        EventDate = e.EventDate,
        Location = e.Location,
        Description = e.Description,
        BannerImageUrl = e.BannerImageUrl,
        WeatherLocationCode = e.WeatherLocationCode,
        EventType = e.EventType,
        DistanceKM = e.DistanceKM,
        CreatedAt = e.CreatedAt
    };
}

/// <summary>Forecast for an event's location (Part 1 plan: location, forecastDate, temperature, conditions, windSpeed).</summary>
public class WeatherResponse
{
    public string Location { get; set; } = string.Empty;
    public DateOnly ForecastDate { get; set; }

    /// <summary>Temperature in degrees Celsius.</summary>
    public double Temperature { get; set; }

    public string Conditions { get; set; } = string.Empty;

    /// <summary>Wind speed in km/h.</summary>
    public double WindSpeed { get; set; }
}

/// <summary>Route details for an event (Part 1 plan: routeName, distanceKM, elevationGainM, mapURL).</summary>
public class RouteRequest
{
    [Required, StringLength(100)]
    public string RouteName { get; set; } = string.Empty;

    [Required, Range(0.1, 9999)]
    public decimal? DistanceKM { get; set; }

    /// <summary>Total climb in metres. Defaults to 0.</summary>
    [Range(0, 10000)]
    public int? ElevationGainM { get; set; }

    [Url, StringLength(255)]
    public string? MapURL { get; set; }
}

public class RouteResponse
{
    public int RouteId { get; set; }
    public int EventId { get; set; }
    public string RouteName { get; set; } = string.Empty;
    public decimal DistanceKM { get; set; }
    public int ElevationGainM { get; set; }
    public string? MapURL { get; set; }

    public static readonly Expression<Func<RaceRoute, RouteResponse>> Projection = r => new RouteResponse
    {
        RouteId = r.RouteId,
        EventId = r.EventId,
        RouteName = r.RouteName,
        DistanceKM = r.DistanceKM,
        ElevationGainM = r.ElevationGainM,
        MapURL = r.MapURL
    };
}

/// <summary>
/// A category is an individual race within an event, e.g. "10km Fun Run" or "Under 20"
/// (Part 1 plan: categoryName, distanceKM, maxParticipants, entryFee).
/// </summary>
public class CategoryRequest
{
    /// <summary>Name such as "Under 20", "Senior", "10km" or "21km". Unique within the event.</summary>
    [Required, StringLength(50)]
    public string CategoryName { get; set; } = string.Empty;

    [Required, Range(0.1, 9999)]
    public decimal? DistanceKM { get; set; }

    /// <summary>Capacity of the category. Defaults to 100.</summary>
    [Range(1, 1000000)]
    public int? MaxParticipants { get; set; }

    /// <summary>Entry fee in rand. Defaults to 0.</summary>
    [Range(0, 100000)]
    public decimal? EntryFee { get; set; }
}

public class CategoryResponse
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal DistanceKM { get; set; }
    public int MaxParticipants { get; set; }
    public decimal EntryFee { get; set; }

    /// <summary>Number of non-cancelled enrolments.</summary>
    public int EnrolledCount { get; set; }

    public static readonly Expression<Func<Category, CategoryResponse>> Projection = c => new CategoryResponse
    {
        CategoryId = c.CategoryId,
        EventId = c.EventId,
        CategoryName = c.CategoryName,
        DistanceKM = c.DistanceKM,
        MaxParticipants = c.MaxParticipants,
        EntryFee = c.EntryFee,
        EnrolledCount = c.Enrolments.Count(en => en.Status != EnrolmentStatus.Cancelled)
    };
}
