using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace dotnet_server;

[AllowAnonymous]
public abstract class PublicPageModel(
    DynamicPages dynamicPages,
    ThemeCatalog themeCatalog,
    ILogger logger) : PageModel
{
    public DynamicPage? CurrentPage { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string LayoutPath { get; private set; } = "~/Themes/default/_Layout.cshtml";

    protected async Task<IActionResult> LoadPageAsync(string slug, CancellationToken cancellationToken)
    {
        try
        {
            CurrentPage = await dynamicPages.FindBySlugAsync(slug, cancellationToken);
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while loading public page {PageSlug}.", slug);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while loading public page {PageSlug}.", slug);
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        if (CurrentPage is null)
        {
            return NotFound();
        }

        var userRole = User.FindFirstValue(ClaimTypes.Role) ?? "Unregistered";
        var userLevel = RoleLevel(userRole);
        var isEditor = userLevel >= RoleLevel("Editor");
        if ((CurrentPage.Status != "Published" || CurrentPage.PublishAt > DateTime.Now) && !isEditor)
        {
            return NotFound();
        }

        if (userLevel < RoleLevel(CurrentPage.MinimumRole))
        {
            return User.Identity?.IsAuthenticated == true
                ? Forbid()
                : Redirect($"/admin/login?ReturnUrl={Uri.EscapeDataString(Request.Path + Request.QueryString)}");
        }

        try
        {
            LayoutPath = themeCatalog.GetLayoutPath(CurrentPage);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Theme configuration error for page {PageSlug}.", slug);
            return ThemeUnavailable();
        }

        ViewData["Title"] = CurrentPage.Title + " - Radix";
        return Page();
    }

    private IActionResult ThemeUnavailable()
    {
        CurrentPage = null;
        return StatusCode(StatusCodes.Status500InternalServerError);
    }

    private static int RoleLevel(string role) => role switch
    {
        "Administrator" => 5,
        "Manager" => 4,
        "Editor" => 3,
        "Registered" => 2,
        _ => 1
    };
}
