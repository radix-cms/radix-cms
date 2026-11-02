using System.Text.RegularExpressions;

namespace dotnet_server;

public sealed class ThemeCatalog(
    IWebHostEnvironment environment,
    ILogger<ThemeCatalog> logger)
{
    private static readonly Regex ValidThemeName = new(
        @"\A[a-z0-9_-]+\z",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public string ThemesPath
    {
        get
        {
            var contentRootThemes = Path.Combine(environment.ContentRootPath, "Themes");
            return Directory.Exists(contentRootThemes)
                ? contentRootThemes
                : Path.Combine(AppContext.BaseDirectory, "Themes");
        }
    }

    public IReadOnlyList<string> GetInstalledThemes()
    {
        if (!Directory.Exists(ThemesPath))
        {
            return [];
        }

        var themes = Directory.EnumerateDirectories(ThemesPath)
            .Select(Path.GetFileName)
            .Where(name => name is not null && ValidThemeName.IsMatch(name))
            .Select(name => name!)
            .Where(name => File.Exists(GetLayoutFilePath(name)))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return themes;
    }

    public string GetLayoutPath(DynamicPage page)
    {
        var installedThemes = GetInstalledThemes();
        var requestedTheme = page.TemplateName.Trim().ToLowerInvariant();
        if (installedThemes.Contains(requestedTheme, StringComparer.OrdinalIgnoreCase))
        {
            return GetLayoutViewPath(requestedTheme);
        }

        logger.LogWarning(
            "Theme {RequestedTheme} for page {PageSlug} is not installed; falling back to the default theme.",
            page.TemplateName,
            page.Slug);
        if (!installedThemes.Contains("default", StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("No default theme is installed. Add Themes/default/_Layout.cshtml.");
        }

        return GetLayoutViewPath("default");
    }

    private string GetLayoutFilePath(string themeName) => Path.Combine(ThemesPath, themeName, "_Layout.cshtml");

    private static string GetLayoutViewPath(string themeName) => $"~/Themes/{themeName}/_Layout.cshtml";
}
