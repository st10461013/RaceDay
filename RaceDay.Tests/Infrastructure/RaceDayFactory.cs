using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Services;

namespace RaceDay.Tests.Infrastructure;

/// <summary>
/// Hosts the real API in memory for integration tests. SQL Server is swapped for an isolated EF Core
/// in-memory database and the live weather provider for a fake, so the tests need no external
/// infrastructure or internet access (ideal for CI).
/// </summary>
public class RaceDayFactory : WebApplicationFactory<Program>
{
    public const string OrganiserCode = "TEST-ORGANISER-CODE";

    private readonly string _databaseName = $"RaceDayTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Auth:OrganiserAccessCode", OrganiserCode);

        builder.ConfigureServices(services =>
        {
            foreach (var descriptor in services
                         .Where(d => d.ServiceType == typeof(DbContextOptions<RaceDayDbContext>))
                         .ToList())
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<RaceDayDbContext>(options => options.UseInMemoryDatabase(_databaseName));

            foreach (var descriptor in services.Where(d => d.ServiceType == typeof(IWeatherService)).ToList())
            {
                services.Remove(descriptor);
            }

            services.AddSingleton<IWeatherService, FakeWeatherService>();
        });
    }
}

/// <summary>Weather code ZA-CT works, ZA-DOWN simulates an outage, anything else is unknown.</summary>
public sealed class FakeWeatherService : IWeatherService
{
    public Task<WeatherResponse?> GetForecastAsync(string locationCode, DateOnly date, CancellationToken cancellationToken)
    {
        if (locationCode == "ZA-DOWN")
        {
            throw new WeatherUnavailableException("Simulated outage.");
        }

        if (locationCode != "ZA-CT")
        {
            return Task.FromResult<WeatherResponse?>(null);
        }

        return Task.FromResult<WeatherResponse?>(new WeatherResponse
        {
            Location = "Cape Town, Western Cape",
            ForecastDate = date,
            Temperature = 21.5,
            Conditions = "Partly cloudy",
            WindSpeed = 14.2
        });
    }
}
