using System.Globalization;
using System.Text.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Api.Services;

/// <summary>Thrown when the external forecast service cannot be reached or returns something unusable.</summary>
public sealed class WeatherUnavailableException : Exception
{
    public WeatherUnavailableException(string message, Exception? inner = null) : base(message, inner)
    {
    }
}

public interface IWeatherService
{
    /// <summary>
    /// Gets the forecast for a location code (a key under "Weather:Locations" in configuration, or "lat,lon").
    /// Returns null when the code is unknown. Throws <see cref="WeatherUnavailableException"/> when the
    /// forecast provider cannot be reached.
    /// </summary>
    Task<WeatherResponse?> GetForecastAsync(string locationCode, DateOnly date, CancellationToken cancellationToken);
}

/// <summary>
/// Live forecasts from Open-Meteo (free, no API key). An event's WeatherLocationCode (e.g. "ZA-CT") is
/// resolved to coordinates through the "Weather:Locations" configuration section.
/// Open-Meteo forecasts up to 16 days ahead; for later (or past) dates the current conditions are returned instead.
/// </summary>
public sealed class OpenMeteoWeatherService : IWeatherService
{
    private const int ForecastHorizonDays = 15;

    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public OpenMeteoWeatherService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public async Task<WeatherResponse?> GetForecastAsync(string locationCode, DateOnly date, CancellationToken cancellationToken)
    {
        var place = Resolve(locationCode);
        if (place is null)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var useForecast = date >= today && date <= today.AddDays(ForecastHorizonDays);

        var lat = place.Value.Latitude.ToString(CultureInfo.InvariantCulture);
        var lon = place.Value.Longitude.ToString(CultureInfo.InvariantCulture);

        var url = useForecast
            ? $"v1/forecast?latitude={lat}&longitude={lon}&daily=weather_code,temperature_2m_max,wind_speed_10m_max" +
              $"&timezone=auto&start_date={date:yyyy-MM-dd}&end_date={date:yyyy-MM-dd}"
            : $"v1/forecast?latitude={lat}&longitude={lon}&current=weather_code,temperature_2m,wind_speed_10m&timezone=auto";

        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (useForecast)
            {
                var daily = root.GetProperty("daily");
                return new WeatherResponse
                {
                    Location = place.Value.Name,
                    ForecastDate = date,
                    Temperature = daily.GetProperty("temperature_2m_max")[0].GetDouble(),
                    WindSpeed = daily.GetProperty("wind_speed_10m_max")[0].GetDouble(),
                    Conditions = Describe(daily.GetProperty("weather_code")[0].GetInt32())
                };
            }

            var current = root.GetProperty("current");
            return new WeatherResponse
            {
                Location = place.Value.Name,
                ForecastDate = today,
                Temperature = current.GetProperty("temperature_2m").GetDouble(),
                WindSpeed = current.GetProperty("wind_speed_10m").GetDouble(),
                Conditions = Describe(current.GetProperty("weather_code").GetInt32())
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException
                                       or KeyNotFoundException or InvalidOperationException or IndexOutOfRangeException
                                       && !cancellationToken.IsCancellationRequested)
        {
            throw new WeatherUnavailableException("The weather service could not be reached.", ex);
        }
    }

    private (string Name, double Latitude, double Longitude)? Resolve(string code)
    {
        double lat = 0, lon = 0;

        var section = _config.GetSection($"Weather:Locations:{code.Trim()}");
        if (section.Exists()
            && double.TryParse(section["Latitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out lat)
            && double.TryParse(section["Longitude"], NumberStyles.Float, CultureInfo.InvariantCulture, out lon))
        {
            return (section["Name"] ?? code, lat, lon);
        }

        // Also accept raw coordinates such as "-33.92,18.42".
        var parts = code.Split(',');
        if (parts.Length == 2
            && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out lat)
            && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out lon)
            && lat is >= -90 and <= 90 && lon is >= -180 and <= 180)
        {
            return ($"{lat:0.####}, {lon:0.####}", lat, lon);
        }

        return null;
    }

    /// <summary>Translates a WMO weather code into plain words.</summary>
    private static string Describe(int code) => code switch
    {
        0 => "Clear sky",
        1 => "Mainly clear",
        2 => "Partly cloudy",
        3 => "Overcast",
        45 or 48 => "Fog",
        >= 51 and <= 57 => "Drizzle",
        >= 61 and <= 67 => "Rain",
        >= 71 and <= 77 => "Snow",
        >= 80 and <= 82 => "Rain showers",
        85 or 86 => "Snow showers",
        95 => "Thunderstorm",
        96 or 99 => "Thunderstorm with hail",
        _ => "Unknown"
    };
}
