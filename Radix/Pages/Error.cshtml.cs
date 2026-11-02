using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace dotnet_server.Pages;

public sealed class ErrorModel(
    ILogger<ErrorModel> logger) : PageModel
{
    public int ErrorStatusCode { get; private set; }
    public string Title { get; private set; } = "Request unavailable";
    public string Message { get; private set; } = "We could not complete your request. Please try again in a moment.";

    public IActionResult OnGet(int statusCode)
    {
        ErrorStatusCode = statusCode is >= 400 and <= 599 ? statusCode : StatusCodes.Status500InternalServerError;
        Response.StatusCode = ErrorStatusCode;

        var exception = HttpContext.Features.Get<IExceptionHandlerPathFeature>()?.Error;
        if (exception is not null)
        {
            logger.LogError(exception, "Unhandled server error while processing {Path}.",
                HttpContext.Features.Get<IExceptionHandlerPathFeature>()?.Path);
        }

        (Title, Message) = ErrorStatusCode switch
        {
            StatusCodes.Status403Forbidden =>
                ("Access denied", "You do not have permission to view this page."),
            StatusCodes.Status404NotFound =>
                ("Page not found", "The page may have moved, or the address may be incorrect."),
            StatusCodes.Status500InternalServerError =>
                ("Something went wrong", "We are experiencing a temporary server issue. Please try again later."),
            _ =>
                ("Service temporarily unavailable", "We could not complete your request. Please try again in a moment.")
        };

        return Page();
    }
}
