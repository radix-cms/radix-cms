using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace dotnet_server.Pages.Admin;

[Authorize]
public sealed class LogoutModel : PageModel
{
    public async Task<IActionResult> OnPostAsync()
    {
        await HttpContext.SignOutAsync();
        return Redirect("/admin/login");
    }
}
