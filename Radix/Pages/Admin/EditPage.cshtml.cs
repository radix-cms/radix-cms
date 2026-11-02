using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace dotnet_server.Pages.Admin;

[Authorize(Policy = "ContentEditor")]
public sealed class EditPageModel(
    DynamicPages dynamicPages,
    RadixDatabase database,
    ThemeCatalog themeCatalog,
    ILogger<EditPageModel> logger) : PageModel
{
    public IReadOnlyList<string> AvailableThemes { get; private set; } = [];

    [BindProperty]
    public int PageId { get; set; }

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
    public DateTime PublishAt { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        AvailableThemes = themeCatalog.GetInstalledThemes();
        var page = await LoadPageAsync(id, cancellationToken);
        if (page is null)
        {
            return NotFound();
        }

        SetPageFields(page);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
    {
        AvailableThemes = themeCatalog.GetInstalledThemes();
        PageId = id;
        Slug = Slug.Trim().ToLowerInvariant();
        Title = Title.Trim();
        TemplateName = string.IsNullOrWhiteSpace(TemplateName) ? "default" : TemplateName.Trim();

        if (!Regex.IsMatch(Slug, @"\A[a-z0-9]+(?:-[a-z0-9]+)*\z", RegexOptions.CultureInvariant)
            || PageEditorOptions.ReservedSlugs.Contains(Slug, StringComparer.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(Slug), "Enter a valid URL slug that is not reserved for an application route.");
        }
        if (!PageEditorOptions.Statuses.Contains(Status, StringComparer.Ordinal) && Status != "Deleted")
        {
            ModelState.AddModelError(nameof(Status), "Select a valid page status.");
        }
        if (!UserRoles.IsValid(MinimumRole))
        {
            ModelState.AddModelError(nameof(MinimumRole), "Select a valid minimum access role.");
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
            if (await dynamicPages.SlugExistsAsync(connection, Slug, id, cancellationToken))
            {
                ErrorMessage = "Another page already uses that URL slug.";
                return Page();
            }

            await using var command = new MySqlCommand(
                "UPDATE pages SET title = @title, slug = @slug, content = @content, template_name = @template, status = @status, min_role = @minRole, publish_at = @publishAt WHERE id = @id",
                connection);
            command.Parameters.AddWithValue("@title", Title);
            command.Parameters.AddWithValue("@slug", Slug);
            command.Parameters.AddWithValue("@content", PageContent ?? string.Empty);
            command.Parameters.AddWithValue("@template", TemplateName);
            command.Parameters.AddWithValue("@status", Status);
            command.Parameters.AddWithValue("@minRole", MinimumRole);
            command.Parameters.AddWithValue("@publishAt", PublishAt);
            command.Parameters.AddWithValue("@id", id);

            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0
                && await dynamicPages.FindByIdAsync(id, cancellationToken) is null)
            {
                return NotFound();
            }

            TempData["StatusMessage"] = "The page was updated.";
            return Redirect("/admin/pages");
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while updating dynamic page {PageId}.", id);
            Response.StatusCode = exception.Number == 1062
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = exception.Number == 1062
                ? "Another page already uses that URL slug."
                : "The page could not be updated. Check the shared database configuration.";
            return Page();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while updating dynamic page {PageId}.", id);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "The shared page database is not configured.";
            return Page();
        }
    }

    private async Task<DynamicPage?> LoadPageAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            return await dynamicPages.FindByIdAsync(id, cancellationToken);
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while loading page {PageId} for editing.", id);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "Could not load this page from the shared database.";
            return null;
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while loading page {PageId} for editing.", id);
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "The shared page database is not configured.";
            return null;
        }
    }

    private void SetPageFields(DynamicPage page)
    {
        PageId = page.Id;
        Title = page.Title;
        Slug = page.Slug;
        PageContent = page.Content;
        Status = page.Status;
        MinimumRole = page.MinimumRole;
        TemplateName = AvailableThemes.Contains(page.TemplateName, StringComparer.OrdinalIgnoreCase)
            ? page.TemplateName
            : "default";
        PublishAt = page.PublishAt;
    }
}
