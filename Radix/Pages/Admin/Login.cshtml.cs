using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace dotnet_server.Pages.Admin;

[AllowAnonymous]
public sealed class LoginModel(RadixDatabase database, ILogger<LoginModel> logger) : PageModel
{
    [BindProperty]
    [Required]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Enter your username and password.";
            return Page();
        }

        try
        {
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "SELECT id, username, email, password_hash, role FROM users WHERE username = @identity OR email = @identity LIMIT 1",
                connection);
            command.Parameters.AddWithValue("@identity", Username.Trim());

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                ErrorMessage = "Invalid username or password.";
                return Page();
            }

            var userId = reader.GetInt32("id");
            var username = reader.GetString("username");
            var email = reader.IsDBNull(reader.GetOrdinal("email")) ? string.Empty : reader.GetString("email");
            var passwordHash = reader.GetString("password_hash");
            var role = reader.GetString("role");
            var match = IsValidBcryptHash(passwordHash) && BCrypt.Net.BCrypt.Verify(Password, passwordHash);

            if (!match || role == "Unregistered")
            {
                ErrorMessage = "Invalid username or password.";
                return Page();
            }

            await reader.CloseAsync();
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId.ToString()),
                new(ClaimTypes.Name, username),
                new(ClaimTypes.Role, role)
            };
            if (!string.IsNullOrEmpty(email))
            {
                claims.Add(new Claim(ClaimTypes.Email, email));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            return !string.IsNullOrEmpty(ReturnUrl) && Url.IsLocalUrl(ReturnUrl)
                ? LocalRedirect(ReturnUrl)
                : Redirect("/admin");
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while signing in to the admin area.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "The account database is unavailable. Check the shared Node/.NET database configuration.";
            return Page();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while signing in to the admin area.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "The account database is not configured. Check the shared Node/.NET database configuration.";
            return Page();
        }
    }

    private static bool IsValidBcryptHash(string hash)
    {
        if (!Regex.IsMatch(hash, @"\A\$2[aby]\$\d{2}\$[./A-Za-z0-9]{53}\z", RegexOptions.CultureInvariant))
        {
            return false;
        }

        return int.TryParse(hash.AsSpan(4, 2), out var workFactor) && workFactor is >= 4 and <= 16;
    }
}
