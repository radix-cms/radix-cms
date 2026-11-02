using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace dotnet_server.Pages;

public sealed class DynamicPageModel(
    DynamicPages dynamicPages,
    ThemeCatalog themeCatalog,
    ILogger<DynamicPageModel> logger) : PublicPageModel(dynamicPages, themeCatalog, logger)
{
    public Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var slug = Request.Path.Value?.Trim('/') ?? string.Empty;
        return LoadPageAsync(slug, cancellationToken);
    }
}
