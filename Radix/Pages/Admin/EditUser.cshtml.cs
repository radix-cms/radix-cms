using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace dotnet_server.Pages.Admin;

[Authorize(Policy = "Administrator")]
public sealed class EditUserModel(
    RadixDatabase database,
    ILogger<EditUserModel> logger) : PageModel
{
    [BindProperty]
    public int UserId { get; set; }

    [BindProperty]
    [StringLength(100)]
    [Required]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    [StringLength(255)]
    public string? FullName { get; set; }

    [BindProperty]
    [StringLength(255)]
    [EmailAddress]
    public string? Email { get; set; }

    [BindProperty]
    [Required]
    public string Role { get; set; } = "Registered";

    [BindProperty]
    public string? Password { get; set; }

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        UserId = id;
        if (!await LoadUserAsync(id, cancellationToken))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id, CancellationToken cancellationToken)
    {
        UserId = id;
        Username = Username.Trim();
        FullName = string.IsNullOrWhiteSpace(FullName) ? null : FullName.Trim();
        Email = string.IsNullOrWhiteSpace(Email) ? null : Email.Trim();
        Password = string.IsNullOrWhiteSpace(Password) ? null : Password;

        if (id <= 0 || !UserRoles.IsValid(Role))
        {
            ErrorMessage = "Enter a valid username and select a valid role.";
            ModelState.AddModelError(nameof(Role), ErrorMessage);
            return Page();
        }

        if (!ModelState.IsValid)
        {
            ErrorMessage = "Check the entered username, name, and email values.";
            return Page();
        }

        try
        {
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            if (await UsersModel.UserExistsAsync(connection, Username, Email, id, cancellationToken))
            {
                ErrorMessage = "Another user with that username or email already exists.";
                return Page();
            }

            if (Password is not null)
            {
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(Password, 10);
                await using var command = new MySqlCommand(
                    "UPDATE users SET username = @username, email = @email, full_name = @fullName, role = @role, password_hash = @passwordHash WHERE id = @id",
                    connection);
                AddUserParameters(command, id);
                command.Parameters.AddWithValue("@passwordHash", passwordHash);
                if (await command.ExecuteNonQueryAsync(cancellationToken) == 0
                    && !await UserExistsByIdAsync(connection, id, cancellationToken))
                {
                    return NotFound();
                }
            }
            else
            {
                await using var command = new MySqlCommand(
                    "UPDATE users SET username = @username, email = @email, full_name = @fullName, role = @role WHERE id = @id",
                    connection);
                AddUserParameters(command, id);
                if (await command.ExecuteNonQueryAsync(cancellationToken) == 0
                    && !await UserExistsByIdAsync(connection, id, cancellationToken))
                {
                    return NotFound();
                }
            }

            if (User.FindFirstValue(ClaimTypes.NameIdentifier) == id.ToString())
            {
                if (Role != "Administrator")
                {
                    await HttpContext.SignOutAsync();
                    return Redirect("/admin/login");
                }

                var claims = new List<Claim>
                {
                    new(ClaimTypes.NameIdentifier, id.ToString()),
                    new(ClaimTypes.Name, Username),
                    new(ClaimTypes.Role, Role)
                };
                if (Email is not null)
                {
                    claims.Add(new Claim(ClaimTypes.Email, Email));
                }

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(new ClaimsIdentity(
                        claims,
                        CookieAuthenticationDefaults.AuthenticationScheme)));
            }

            TempData["StatusMessage"] = "The user account was updated.";
            return Redirect("/admin/users");
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while editing a user account.");
            ErrorMessage = exception.Number == 1062
                ? "Another user with that username or email already exists."
                : "The user account could not be updated. Check the shared database configuration.";
            Response.StatusCode = exception.Number == 1062
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status503ServiceUnavailable;
            return Page();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while editing a user account.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "The shared account database is not configured. Check the .NET and Node database configuration.";
            return Page();
        }
    }

    private void AddUserParameters(MySqlCommand command, int id)
    {
        command.Parameters.AddWithValue("@username", Username);
        command.Parameters.AddWithValue("@email", (object?)Email ?? DBNull.Value);
        command.Parameters.AddWithValue("@fullName", (object?)FullName ?? DBNull.Value);
        command.Parameters.AddWithValue("@role", Role);
        command.Parameters.AddWithValue("@id", id);
    }

    private async Task<bool> LoadUserAsync(int id, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "SELECT username, email, full_name, role FROM users WHERE id = @id",
                connection);
            command.Parameters.AddWithValue("@id", id);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return false;
            }

            Username = reader.GetString("username");
            Email = reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString("email");
            FullName = reader.IsDBNull(reader.GetOrdinal("full_name")) ? null : reader.GetString("full_name");
            Role = reader.GetString("role");
            return true;
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while loading a user account for editing.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "Could not load this account from the shared database.";
            return false;
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while loading a user account for editing.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "The shared account database is not configured. Check the .NET and Node database configuration.";
            return false;
        }
    }

    private static async Task<bool> UserExistsByIdAsync(
        MySqlConnection connection,
        int id,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand("SELECT COUNT(*) FROM users WHERE id = @id", connection);
        command.Parameters.AddWithValue("@id", id);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }
}
