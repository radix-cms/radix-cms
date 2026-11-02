using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace dotnet_server.Pages.Admin;

[Authorize(Policy = "ContentEditor")]
public sealed class ThemesModel(
    DynamicPages dynamicPages,
    RadixDatabase database,
    ThemeCatalog themeCatalog,
    ILogger<ThemesModel> logger) : PageModel
{
    public IReadOnlyList<string> Themes { get; private set; } = [];

    public IReadOnlyList<DynamicPage> Pages { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Themes = themeCatalog.GetInstalledThemes();
        try
        {
            Pages = await dynamicPages.GetPagesAsync(false, cancellationToken);
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while loading pages for theme assignment.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "Could not load pages from the shared database.";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while loading pages for theme assignment.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "The shared page database is not configured.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAssignAsync(
        int pageId,
        string themeName,
        CancellationToken cancellationToken)
    {
        var installedThemes = themeCatalog.GetInstalledThemes();
        if (pageId <= 0 || !installedThemes.Contains(themeName, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest("Choose a page and an installed theme.");
        }

        try
        {
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "UPDATE pages SET template_name = @theme WHERE id = @id AND status <> 'Deleted'",
                connection);
            command.Parameters.AddWithValue("@theme", themeName);
            command.Parameters.AddWithValue("@id", pageId);

            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0
                && await dynamicPages.FindByIdAsync(pageId, cancellationToken) is null)
            {
                return NotFound();
            }

            TempData["StatusMessage"] = "The page theme was updated.";
            return Redirect("/admin/themes");
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while assigning theme to page {PageId}.", pageId);
            return DatabaseUnavailable();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while assigning theme to page {PageId}.", pageId);
            return DatabaseUnavailable();
        }
    }

    private IActionResult DatabaseUnavailable()
    {
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return Content("The shared page database is unavailable. Check the .NET and Node database configuration.");
    }
}
