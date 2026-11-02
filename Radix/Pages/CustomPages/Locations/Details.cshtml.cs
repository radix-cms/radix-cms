using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using dotnet_server.CustomCode;

namespace dotnet_server.Pages.CustomPages.Locations;

public sealed class DetailsModel(
    BranchLocations branchLocations,
    ILogger<DetailsModel> logger) : PageModel
{
    public BranchLocation? Location { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken cancellationToken)
    {
        try
        {
            Location = await branchLocations.FindBySlugAsync(slug, cancellationToken);
        }
        catch (IOException)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }
        catch (InvalidDataException)
        {
            return StatusCode(StatusCodes.Status500InternalServerError);
        }

        if (Location is null)
        {
            logger.LogInformation("Branch location {Slug} was not found.", slug);
            return NotFound();
        }

        return Page();
    }

    public string PhoneLink(string phone)
        => new(phone.Where(character => character == '+' || char.IsDigit(character)).ToArray());
}
