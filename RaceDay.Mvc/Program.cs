using RaceDay.Mvc.Services;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews().AddJsonOptions(o => o.JsonSerializerOptions.PropertyNameCaseInsensitive = true);
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o => { o.Cookie.Name = ".RaceDay.Mvc"; o.Cookie.HttpOnly = true; o.Cookie.IsEssential = true; o.Cookie.SameSite = SameSiteMode.Lax; o.IdleTimeout = TimeSpan.FromMinutes(30); });
builder.Services.AddHttpClient("RaceDayApi", c => c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5000/"));
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ApiClient>();
var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Home/Error"); app.UseHsts(); }
app.UseHttpsRedirection(); app.UseStaticFiles(); app.UseRouting(); app.UseSession();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
