using System.Reflection;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Database (Entity Framework Core, code-first, SQL Server) ---
builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHostedService<DatabaseInitializer>();

// --- Security ---
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

// --- Weather (external forecast service used by GET /api/events/{eventId}/weather) ---
builder.Services.AddHttpClient<IWeatherService, OpenMeteoWeatherService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Weather:BaseUrl"] ?? "https://api.open-meteo.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

// --- Session management: keeps the logged-in user's id and role between requests ---
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.IdleTimeout = TimeSpan.FromMinutes(
        builder.Configuration.GetValue("Auth:SessionIdleTimeoutMinutes", 30));
});

// --- Controllers & JSON (enums travel as readable strings, e.g. "Organiser") ---
builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// --- Swagger ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description = "RESTful API for the RaceDay event system. " +
                      "Authentication is session based: call POST /api/auth/login first, " +
                      "after which the browser keeps the session cookie and every endpoint can be tried out here. " +
                      "Endpoints marked Public need no login."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
    options.DocumentTitle = "RaceDay API";
});

if (!builder.Environment.IsEnvironment("Container")) app.UseHttpsRedirection();
app.UseSession();
app.MapControllers();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

// Exposed so the integration tests can host the API with WebApplicationFactory<Program>.
public partial class Program { }
