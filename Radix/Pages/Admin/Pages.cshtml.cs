using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace dotnet_server.Pages.Admin;

[Authorize(Policy = "ContentEditor")]
public sealed class PagesModel(
    DynamicPages dynamicPages,
    RadixDatabase database,
    IActionDescriptorCollectionProvider actionDescriptors,
    ILogger<PagesModel> logger) : PageModel
{
    public IReadOnlyList<DynamicPage> Pages { get; private set; } = [];

    public IReadOnlySet<string> StaticPageSlugs { get; private set; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public bool ShowTrash => string.Equals(Request.Query["view"], "trash", StringComparison.OrdinalIgnoreCase);

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            Pages = await dynamicPages.GetPagesAsync(ShowTrash, cancellationToken);
            StaticPageSlugs = actionDescriptors.ActionDescriptors.Items
                .OfType<PageActionDescriptor>()
                .Select(page => page.RelativePath
                    .Replace('\\', '/')
                    .TrimStart('/')
                    .Split('/'))
                .Where(parts => parts.Length == 2
                    && parts[0].Equals("Pages", StringComparison.OrdinalIgnoreCase)
                    && parts[1].EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase))
                .Select(parts => Path.GetFileNameWithoutExtension(parts[1]))
                .Where(slug => !new[] { "Index", "About", "DynamicPage" }
                    .Contains(slug, StringComparer.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while listing dynamic pages.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "Could not load pages from the shared database.";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while listing dynamic pages.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "The shared page database is not configured.";
        }

        return Page();
    }

    public Task<IActionResult> OnPostTrashAsync(int pageId, CancellationToken cancellationToken)
        => UpdateStatusAsync(pageId, "Deleted", "The page was moved to trash.", cancellationToken);

    public Task<IActionResult> OnPostRestoreAsync(int pageId, CancellationToken cancellationToken)
        => UpdateStatusAsync(pageId, "Draft", "The page was restored as a draft.", cancellationToken);

    public async Task<IActionResult> OnPostDeletePermanentlyAsync(int pageId, CancellationToken cancellationToken)
    {
        if (!User.IsInRole("Administrator"))
        {
            return Forbid();
        }

        if (pageId <= 0)
        {
            return BadRequest();
        }

        try
        {
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "DELETE FROM pages WHERE id = @id AND status = 'Deleted'",
                connection);
            command.Parameters.AddWithValue("@id", pageId);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                return NotFound();
            }

            TempData["StatusMessage"] = "The page was permanently deleted.";
            return Redirect("/admin/pages?view=trash");
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while permanently deleting page {PageId}.", pageId);
            return DatabaseUnavailable();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while permanently deleting page {PageId}.", pageId);
            return DatabaseUnavailable();
        }
    }

    private async Task<IActionResult> UpdateStatusAsync(
        int pageId,
        string status,
        string successMessage,
        CancellationToken cancellationToken)
    {
        if (pageId <= 0)
        {
            return BadRequest();
        }

        try
        {
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "UPDATE pages SET status = @status WHERE id = @id",
                connection);
            command.Parameters.AddWithValue("@status", status);
            command.Parameters.AddWithValue("@id", pageId);
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                await using var check = new MySqlCommand("SELECT COUNT(*) FROM pages WHERE id = @id", connection);
                check.Parameters.AddWithValue("@id", pageId);
                if (Convert.ToInt32(await check.ExecuteScalarAsync(cancellationToken)) == 0)
                {
                    return NotFound();
                }
            }

            TempData["StatusMessage"] = successMessage;
            return Redirect("/admin/pages" + (status == "Deleted" ? string.Empty : "?view=trash"));
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while updating page {PageId} status.", pageId);
            return DatabaseUnavailable();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while updating page {PageId} status.", pageId);
            return DatabaseUnavailable();
        }
    }

    private IActionResult DatabaseUnavailable()
    {
        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return Content("The shared page database is unavailable. Check the .NET and Node database configuration.");
    }
}
