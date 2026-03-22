using AINews.Api.BackgroundServices;
using AINews.Api.Data;
using AINews.Api.Hubs;
using AINews.Api.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Data Protection (persisted to volume in production)
var keysPath = builder.Configuration["DataProtection:KeyPath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "keys");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("AINews");

// Database
var dbPath = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "data", "ainews.db")}";
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(dbPath));

// Authentication
builder.Services.AddAuthentication(opt =>
{
    opt.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    opt.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(opt =>
{
    opt.Cookie.Name = "ainews.auth";
    opt.Cookie.HttpOnly = true;
    opt.Cookie.SameSite = SameSiteMode.Lax;
    opt.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    opt.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = 401;
        return Task.CompletedTask;
    };
    opt.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = 403;
        return Task.CompletedTask;
    };
})
.AddGoogle(opt =>
{
    opt.ClientId = builder.Configuration["Google:ClientId"] ?? "placeholder";
    opt.ClientSecret = builder.Configuration["Google:ClientSecret"] ?? "placeholder";
    opt.SaveTokens = false;
    opt.CallbackPath = "/api/auth/callback/google";
});

builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddSignalR();

// App services
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<RedditService>();
builder.Services.AddScoped<XService>();
builder.Services.AddScoped<AiService>();
builder.Services.AddScoped<LinkExtractorService>();
builder.Services.AddScoped<ContentFetcherService>();
builder.Services.AddScoped<ScanOrchestrator>();
builder.Services.AddSingleton<ScanBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ScanBackgroundService>());
builder.Services.AddHttpClient();

// CORS for local dev (Vite dev server on port 5173)
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("DevCors", p => p
        .WithOrigins("http://localhost:5173")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

// Ensure data directory exists and auto-migrate
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "data"));
Directory.CreateDirectory(keysPath);
using (var scope = app.Services.CreateScope())
{
    var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await ctx.Database.MigrateAsync();
}

app.UseCors("DevCors");
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ScanProgressHub>("/hubs/scan");

// SPA fallback — serve React app for all non-API routes
app.MapFallbackToFile("index.html");

app.Run();
