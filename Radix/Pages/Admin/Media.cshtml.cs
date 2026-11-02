using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace dotnet_server.Pages.Admin;

[Authorize(Policy = "ContentEditor")]
public sealed class MediaModel(
    IWebHostEnvironment environment,
    ILogger<MediaModel> logger) : PageModel
{
    private const long MaximumFileSize = 10 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, byte[]> ImageSignatures =
        new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = [0xFF, 0xD8, 0xFF],
            [".jpeg"] = [0xFF, 0xD8, 0xFF],
            [".png"] = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A],
            [".gif"] = "GIF8"u8.ToArray(),
            [".webp"] = "RIFF"u8.ToArray()
        };

    public IReadOnlyList<MediaImage> Images { get; private set; } = [];

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        try
        {
            var directory = GetUploadDirectory();
            Directory.CreateDirectory(directory);
            Images = Directory.EnumerateFiles(directory)
                .Select(path => new FileInfo(path))
                .Where(file => ImageSignatures.ContainsKey(file.Extension))
                .OrderByDescending(file => file.LastWriteTimeUtc)
                .Select(file => new MediaImage(
                    file.Name,
                    $"/uploads/{Uri.EscapeDataString(file.Name)}",
                    $"{file.Length / 1024d:0.0} KB"))
                .ToArray();
        }
        catch (IOException exception)
        {
            logger.LogError(exception, "Could not read the media library.");
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            ErrorMessage = "The media library could not be loaded.";
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogError(exception, "Access denied while reading the media library.");
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            ErrorMessage = "The media library could not be loaded.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostUploadAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "No image was uploaded." });
        }
        if (file.Length > MaximumFileSize)
        {
            return BadRequest(new { error = "Images must be 10 MB or smaller." });
        }

        var extension = Path.GetExtension(Path.GetFileName(file.FileName));
        if (!ImageSignatures.TryGetValue(extension, out var signature))
        {
            return BadRequest(new { error = "Only JPG, PNG, GIF, and WEBP images are allowed." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var header = new byte[Math.Max(signature.Length, 12)];
            var bytesRead = await stream.ReadAsync(header, cancellationToken);
            if (!HasValidSignature(extension, signature, header, bytesRead))
            {
                return BadRequest(new { error = "The uploaded file is not a valid image of the selected type." });
            }

            var fileName = $"image-{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var directory = GetUploadDirectory();
            Directory.CreateDirectory(directory);
            var destination = Path.Combine(directory, fileName);
            await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Position = 0;
                await stream.CopyToAsync(output, cancellationToken);
            }

            return new JsonResult(new { location = $"/uploads/{Uri.EscapeDataString(fileName)}" });
        }
        catch (IOException exception)
        {
            logger.LogError(exception, "Failed to save an uploaded media image.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "The image could not be saved." });
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogError(exception, "Access denied while saving an uploaded media image.");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "The image could not be saved." });
        }
    }

    public IActionResult OnPostDelete(string? fileName)
    {
        if (!User.IsInRole("Administrator"))
        {
            return Forbid();
        }
        if (string.IsNullOrWhiteSpace(fileName)
            || !string.Equals(fileName, Path.GetFileName(fileName), StringComparison.Ordinal)
            || !ImageSignatures.ContainsKey(Path.GetExtension(fileName)))
        {
            return BadRequest();
        }

        try
        {
            var filePath = Path.Combine(GetUploadDirectory(), fileName);
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound();
            }

            System.IO.File.Delete(filePath);
            TempData["StatusMessage"] = "The image was deleted.";
            return RedirectToPage();
        }
        catch (IOException exception)
        {
            logger.LogError(exception, "Failed to delete media image {FileName}.", fileName);
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            ErrorMessage = "The image could not be deleted.";
            return OnGet();
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogError(exception, "Access denied while deleting media image {FileName}.", fileName);
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            ErrorMessage = "The image could not be deleted.";
            return OnGet();
        }
    }

    private string GetUploadDirectory()
    {
        var webRoot = environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(environment.ContentRootPath, "wwwroot");
        }

        return Path.Combine(webRoot, "uploads");
    }

    private static bool HasValidSignature(string extension, byte[] signature, byte[] header, int bytesRead)
    {
        if (bytesRead < signature.Length || !header.AsSpan(0, signature.Length).SequenceEqual(signature))
        {
            return false;
        }

        return !extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
            || bytesRead >= 12 && header.AsSpan(8, 4).SequenceEqual("WEBP"u8);
    }
}

public sealed record MediaImage(string Name, string Url, string Size);
