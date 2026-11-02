using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace dotnet_server.Pages.Admin;

public sealed class IndexModel(
    RadixDatabase database,
    IWebHostEnvironment environment,
    IActionDescriptorCollectionProvider actionDescriptors,
    ILogger<IndexModel> logger) : PageModel
{
    private static readonly HashSet<string> ImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    public int? TotalPages { get; private set; }

    public int? PublishedPages { get; private set; }

    public int? DraftPages { get; private set; }

    public int? TotalUsers { get; private set; }

    public int? StaticPagesCount { get; private set; }

    public int? MediaCount { get; private set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            await using var command = new MySqlCommand(
                """
                SELECT
                    (SELECT COUNT(*) FROM pages WHERE status <> 'Deleted') AS total_pages,
                    (SELECT COUNT(*) FROM pages WHERE status = 'Published') AS published_pages,
                    (SELECT COUNT(*) FROM pages WHERE status = 'Draft') AS draft_pages,
                    (SELECT COUNT(*) FROM users) AS total_users
                """,
                connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                TotalPages = Convert.ToInt32(reader["total_pages"]);
                PublishedPages = Convert.ToInt32(reader["published_pages"]);
                DraftPages = Convert.ToInt32(reader["draft_pages"]);
                TotalUsers = Convert.ToInt32(reader["total_users"]);
            }
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while loading admin dashboard statistics.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "Database statistics are unavailable. Check the shared database configuration.";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while loading admin dashboard statistics.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "Database statistics are unavailable. Check the shared database configuration.";
        }

        try
        {
            var excludedPageNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Index.cshtml",
                "About.cshtml",
                "DynamicPage.cshtml"
            };
            StaticPagesCount = actionDescriptors.ActionDescriptors.Items
                .OfType<PageActionDescriptor>()
                .Count(page =>
                {
                    var pathParts = page.RelativePath.Split(
                        ['/', '\\'],
                        StringSplitOptions.RemoveEmptyEntries);
                    return pathParts.Length == 2
                        && pathParts[0].Equals("Pages", StringComparison.OrdinalIgnoreCase)
                        && !excludedPageNames.Contains(pathParts[1]);
                });

            var webRoot = environment.WebRootPath;
            if (string.IsNullOrWhiteSpace(webRoot))
            {
                webRoot = Path.Combine(environment.ContentRootPath, "wwwroot");
            }

            var uploadsDirectory = Path.Combine(webRoot, "uploads");
            MediaCount = Directory.Exists(uploadsDirectory)
                ? Directory.EnumerateFiles(uploadsDirectory)
                    .Count(file => ImageExtensions.Contains(Path.GetExtension(file)))
                : 0;
        }
        catch (IOException exception)
        {
            logger.LogError(exception, "File system error while loading admin dashboard statistics.");
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            ErrorMessage = "File-based dashboard statistics are unavailable.";
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogError(exception, "Access denied while loading admin dashboard statistics.");
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            ErrorMessage = "File-based dashboard statistics are unavailable.";
        }

        return Page();
    }
}
