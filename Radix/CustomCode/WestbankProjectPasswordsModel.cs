using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Data.SqlClient;

namespace dotnet_server.CustomCode;

[Authorize(Roles = "Manager,Administrator")]
public sealed class WestbankProjectPasswordsModel(
    WestbankProjectPasswords projects,
    ILogger<WestbankProjectPasswordsModel> logger) : PageModel
{
    [BindProperty, BindRequired, Range(0, int.MaxValue)]
    public int Account { get; set; }

    [BindProperty, Required, StringLength(255)]
    public string Password { get; set; } = string.Empty;

    [BindProperty, Required, StringLength(255)]
    public string ProjectName { get; set; } = string.Empty;

    public IReadOnlyList<WestbankProject> ProjectList { get; private set; } = [];
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        return await LoadProjectsAsync(cancellationToken) ? Page() : StatusCode(StatusCodes.Status503ServiceUnavailable);
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        if (!ValidateProject())
        {
            ErrorMessage = "Check the account, password, and project name and try again.";
            return await ReturnPageAsync(cancellationToken);
        }

        try
        {
            await projects.CreateAsync(Account, Password.Trim(), ProjectName.Trim(), cancellationToken);
            return RedirectToPage();
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Database error while creating a Westbank project account.");
            ErrorMessage = "The Westbank project account could not be created.";
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return await ReturnPageAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            logger.LogError(exception, "Configuration error while creating a Westbank project account.");
            ErrorMessage = "The Westbank project-password database is not configured correctly.";
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return await ReturnPageAsync(cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostEditAsync(int id, CancellationToken cancellationToken)
    {
        if (id <= 0 || !ValidateProject())
        {
            ErrorMessage = id <= 0
                ? "The selected project account is invalid."
                : "Check the account, password, and project name and try again.";
            return await ReturnPageAsync(cancellationToken);
        }

        try
        {
            if (!await projects.UpdateAsync(id, Account, Password.Trim(), ProjectName.Trim(), cancellationToken))
            {
                ErrorMessage = "The selected project account was not found.";
                Response.StatusCode = StatusCodes.Status404NotFound;
                return await ReturnPageAsync(cancellationToken);
            }

            return RedirectToPage();
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Database error while updating a Westbank project account.");
            ErrorMessage = "The Westbank project account could not be updated.";
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return await ReturnPageAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            logger.LogError(exception, "Configuration error while updating a Westbank project account.");
            ErrorMessage = "The Westbank project-password database is not configured correctly.";
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return await ReturnPageAsync(cancellationToken);
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            ErrorMessage = "The selected project account is invalid.";
            return await ReturnPageAsync(cancellationToken);
        }

        try
        {
            if (!await projects.DeleteAsync(id, cancellationToken))
            {
                ErrorMessage = "The selected project account was not found.";
                Response.StatusCode = StatusCodes.Status404NotFound;
                return await ReturnPageAsync(cancellationToken);
            }

            return RedirectToPage();
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Database error while deleting a Westbank project account.");
            ErrorMessage = "The Westbank project account could not be deleted.";
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return await ReturnPageAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            logger.LogError(exception, "Configuration error while deleting a Westbank project account.");
            ErrorMessage = "The Westbank project-password database is not configured correctly.";
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return await ReturnPageAsync(cancellationToken);
        }
    }

    private bool ValidateProject()
    {
        Password = Password?.Trim() ?? string.Empty;
        ProjectName = ProjectName?.Trim() ?? string.Empty;
        return TryValidateModel(this);
    }

    private async Task<IActionResult> ReturnPageAsync(CancellationToken cancellationToken)
    {
        if (!await LoadProjectsAsync(cancellationToken))
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }

        return Page();
    }

    private async Task<bool> LoadProjectsAsync(CancellationToken cancellationToken)
    {
        try
        {
            ProjectList = await projects.GetAllAsync(cancellationToken);
            return true;
        }
        catch (SqlException exception)
        {
            logger.LogError(exception, "Database error while loading Westbank project accounts.");
            ErrorMessage ??= "Westbank project accounts could not be loaded.";
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException)
        {
            logger.LogError(exception, "Configuration error while loading Westbank project accounts.");
            ErrorMessage ??= "The Westbank project-password database is not configured correctly.";
        }

        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return false;
    }
}
