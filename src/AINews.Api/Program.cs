using AINews.Api.BackgroundServices;
using AINews.Api.Data;
using AINews.Api.Hubs;
using AINews.Api.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Data Protection (persisted to volume in production) — used to encrypt settings in DB
var keysPath = builder.Configuration["DataProtection:KeyPath"]
    .NullIfEmpty() ?? Path.Combine(builder.Environment.ContentRootPath, "keys");
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("AINews");

// Database
var dbPath = builder.Configuration.GetConnectionString("DefaultConnection")
    .NullIfEmpty() ?? $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "data", "ainews.db")}";
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseSqlite(dbPath));

builder.Services.AddControllers();
builder.Services.AddSignalR();

// App services
builder.Services.AddScoped<SettingsService>();
builder.Services.AddScoped<RedditService>();
builder.Services.AddScoped<XService>();
builder.Services.AddScoped<AiService>();
builder.Services.AddScoped<LinkExtractorService>();
builder.Services.AddScoped<ContentFetcherService>();
builder.Services.AddScoped<PreferenceService>();
builder.Services.AddScoped<ScanOrchestrator>();
builder.Services.AddSingleton<ScanBackgroundService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ScanBackgroundService>());
builder.Services.AddHttpClient();

// CORS for local dev (Vite dev server on port 5173)
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("DevCors", p => p
        .WithOrigins("http://localhost:5173", "http://localhost:5174",
                     "http://localhost:5175", "http://localhost:5176")
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
app.MapControllers();
app.MapHub<ScanProgressHub>("/hubs/scan");

// SPA fallback — serve React app for all non-API routes
app.MapFallbackToFile("index.html");

app.Run();

static class StringExtensions
{
    public static string? NullIfEmpty(this string? s) => string.IsNullOrEmpty(s) ? null : s;
}
