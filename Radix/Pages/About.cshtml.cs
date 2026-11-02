using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace dotnet_server.Pages;

public sealed class AboutModel(
    DynamicPages dynamicPages,
    ThemeCatalog themeCatalog,
    ILogger<AboutModel> logger) : PublicPageModel(dynamicPages, themeCatalog, logger)
{
    public Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => LoadPageAsync("about", cancellationToken);
}
