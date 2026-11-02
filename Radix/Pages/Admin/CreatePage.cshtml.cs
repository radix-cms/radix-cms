using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace dotnet_server.Pages.Admin;

[Authorize(Policy = "ContentEditor")]
public sealed class CreatePageModel(
    DynamicPages dynamicPages,
    RadixDatabase database,
    ThemeCatalog themeCatalog,
    ILogger<CreatePageModel> logger) : PageModel
{
    public IReadOnlyList<string> AvailableThemes { get; private set; } = themeCatalog.GetInstalledThemes();

    [BindProperty, Required, StringLength(255)]
    public string Title { get; set; } = string.Empty;

    [BindProperty, Required, StringLength(255)]
    public string Slug { get; set; } = string.Empty;

    [BindProperty]
    public string PageContent { get; set; } = string.Empty;

    [BindProperty, Required]
    public string Status { get; set; } = "Draft";

    [BindProperty, Required]
    public string MinimumRole { get; set; } = "Unregistered";

    [BindProperty, Required, StringLength(50)]
    public string TemplateName { get; set; } = "default";

    [BindProperty]
    public DateTime PublishAt { get; set; } = DateTime.Now;

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        AvailableThemes = themeCatalog.GetInstalledThemes();
        Slug = Slug.Trim().ToLowerInvariant();
        Title = Title.Trim();
        TemplateName = string.IsNullOrWhiteSpace(TemplateName) ? "default" : TemplateName.Trim();

        if (!Regex.IsMatch(Slug, @"\A[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)
            || PageEditorOptions.ReservedSlugs.Contains(Slug, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(Slug), "Enter a valid URL slug that is not reserved for an application route.");
        }
        if (!PageEditorOptions.Statuses.Contains(Status, StringComparer.Ordinal)
            || !UserRoles.IsValid(MinimumRole))
        {
            ModelState.AddModelError(string.Empty, "Select a valid page status and minimum access role.");
        }
        if (!AvailableThemes.Contains(TemplateName, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(TemplateName), "Select an installed theme.");
        }

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Check the page details and try again.";
            return Page();
        }

        try
        {
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            if (await dynamicPages.SlugExistsAsync(connection, Slug, null, cancellationToken))
            {
                ErrorMessage = "A page with that URL slug already exists.";
                return Page();
            }

            await using var command = new MySqlCommand(
                "INSERT INTO pages (title, slug, content, template_name, status, min_role, publish_at, created_at) VALUES (@title, @slug, @content, @template, @status, @minRole, @publishAt, NOW())",
                connection);
            command.Parameters.AddWithValue("@title", Title);
            command.Parameters.AddWithValue("@slug", Slug);
            command.Parameters.AddWithValue("@content", PageContent ?? string.Empty);
            command.Parameters.AddWithValue("@template", TemplateName);
            command.Parameters.AddWithValue("@status", Status);
            command.Parameters.AddWithValue("@minRole", MinimumRole);
            command.Parameters.AddWithValue("@publishAt", PublishAt);
            await command.ExecuteNonQueryAsync(cancellationToken);

            TempData["StatusMessage"] = "The page was created.";
            return Redirect("/admin/pages");
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while creating a dynamic page.");
            Response.StatusCode = exception.Number == 1062
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = exception.Number == 1062
                ? "A page with that URL slug already exists."
                : "The page could not be created. Check the shared database configuration.";
            return Page();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while creating a dynamic page.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "The shared page database is not configured.";
            return Page();
        }
    }
}

public static class PageEditorOptions
{
    public static IReadOnlyList<string> Statuses { get; } =
        ["Published", "Pending Review", "Draft", "Archived"];

    public static IReadOnlyList<string> ReservedSlugs { get; } =
        ["admin", "api", "uploads", "favicon.ico", "css", "js", "dynamicpage"];
}
