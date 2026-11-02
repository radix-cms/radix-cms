using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.FileProviders;
using dotnet_server;
using dotnet_server.CustomCode;

var builder = WebApplication.CreateBuilder(args);

// Local .NET settings take precedence over the legacy Node settings fallback.
var localSettingsPath = Path.Combine(builder.Environment.ContentRootPath, "appsettings.local.json");
if (File.Exists(localSettingsPath))
{
    var localSettings = new ConfigurationBuilder()
        .SetBasePath(builder.Environment.ContentRootPath)
        .AddJsonFile("appsettings.local.json", optional: false)
        .Build();
    foreach (var (key, value) in localSettings.GetSection("mssql_intranet").AsEnumerable())
    {
        if (value is not null && builder.Configuration[key] is null)
        {
            builder.Configuration[key] = value;
        }
    }
}

// Reuse legacy Node settings when available; this is optional because the
// Node server may live elsewhere or no longer contain this configuration.
var nodeSettingsDirectory = Path.GetFullPath(Path.Combine(
    builder.Environment.ContentRootPath,
    "..",
    "node-server"));
var nodeSettingsPath = Path.Combine(nodeSettingsDirectory, "appsettings.json");
if (File.Exists(nodeSettingsPath))
{
    var nodeSettings = new ConfigurationBuilder()
        .SetBasePath(nodeSettingsDirectory)
        .AddJsonFile("appsettings.json", optional: false)
        .Build();
    foreach (var (key, value) in nodeSettings.GetSection("mssql_intranet").AsEnumerable())
    {
        if (value is not null && builder.Configuration[key] is null)
        {
            builder.Configuration[key] = value;
        }
    }
}

// Use the launcher's configured port, defaulting to the .NET engine's standard port.
builder.WebHost.UseUrls($"http://0.0.0.0:{builder.Configuration["PORT"] ?? "5000"}");

builder.Services.AddSingleton<RadixDatabase>();
builder.Services.AddSingleton<DynamicPages>();
builder.Services.AddSingleton<ThemeCatalog>();
builder.Services.AddCustomCode();
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/login";
        options.AccessDeniedPath = "/admin/access-denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Administrator", policy =>
        policy.RequireAuthenticatedUser().RequireRole("Administrator"));
    options.AddPolicy("ContentEditor", policy =>
        policy.RequireAuthenticatedUser().RequireRole("Administrator", "Manager", "Editor"));
});
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin");
    options.Conventions.AllowAnonymousToPage("/Admin/Login");
    options.Conventions.AllowAnonymousToPage("/Admin/AccessDenied");
});

var app = builder.Build();

app.UseExceptionHandler("/Error/500");
app.UseStatusCodePagesWithReExecute("/Error/{0}");
app.UseStaticFiles();
var themesPath = Path.Combine(builder.Environment.ContentRootPath, "Themes");
if (!Directory.Exists(themesPath))
{
    themesPath = Path.Combine(AppContext.BaseDirectory, "Themes");
}
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(themesPath),
    RequestPath = "/themes"
});
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

// JSON API Route
app.MapGet("/api/status", () => Results.Ok(new
{
    status = "online",
    engine = "Radix",
    timestamp = DateTime.UtcNow.ToString("o")
}));

app.MapCustomCode();

app.MapFallbackToPage("/DynamicPage");

app.Run();