using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace dotnet_server.Pages;

public sealed class HomeModel(
    DynamicPages dynamicPages,
    ThemeCatalog themeCatalog,
    ILogger<HomeModel> logger) : PublicPageModel(dynamicPages, themeCatalog, logger)
{
    public Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
        => LoadPageAsync("home", cancellationToken);
}
