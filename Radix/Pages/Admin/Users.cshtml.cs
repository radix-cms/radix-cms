using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySqlConnector;

namespace dotnet_server.Pages.Admin;

[Authorize(Policy = "Administrator")]
public sealed class UsersModel(
    RadixDatabase database,
    ILogger<UsersModel> logger) : PageModel
{
    public List<ManagedUser> Users { get; } = [];

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!await LoadUsersAsync(cancellationToken))
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(
        int userId,
        string? role,
        CancellationToken cancellationToken)
        => await OnPostRoleAsync(userId, role, cancellationToken);

    public async Task<IActionResult> OnPostRoleAsync(
        int userId,
        string? role,
        CancellationToken cancellationToken)
    {
        if (userId <= 0 || !UserRoles.IsValid(role))
        {
            ErrorMessage = "Select a valid role for a valid user account.";
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await LoadUsersAsync(cancellationToken);
            return Page();
        }

        try
        {
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "UPDATE users SET role = @role WHERE id = @id",
                connection);
            command.Parameters.AddWithValue("@role", role);
            command.Parameters.AddWithValue("@id", userId);

            var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
            if (affectedRows == 0)
            {
                await using var checkCommand = new MySqlCommand(
                    "SELECT COUNT(*) FROM users WHERE id = @id",
                    connection);
                checkCommand.Parameters.AddWithValue("@id", userId);
                if (Convert.ToInt32(await checkCommand.ExecuteScalarAsync(cancellationToken)) == 0)
                {
                    return NotFound();
                }
            }

            if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId)
                && currentUserId == userId
                && role != "Administrator")
            {
                await HttpContext.SignOutAsync();
                return Redirect("/admin/login");
            }

            TempData["StatusMessage"] = "The user role was updated.";
            return Redirect("/admin/users");
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while updating a user role.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "The shared account database is unavailable. Check the .NET and Node database configuration.";
            await LoadUsersAsync(cancellationToken);
            return Page();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while updating a user role.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "The shared account database is not configured. Check the .NET and Node database configuration.";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostCreateAsync(
        string? username,
        string? fullName,
        string? email,
        string? password,
        string? role,
        CancellationToken cancellationToken)
    {
        username = username?.Trim();
        fullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName.Trim();
        email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();

        if (string.IsNullOrWhiteSpace(username)
            || username.Length > 100
            || string.IsNullOrWhiteSpace(password)
            || !UserRoles.IsValid(role)
            || (fullName is not null && fullName.Length > 255)
            || (email is not null && (email.Length > 255 || !System.Net.Mail.MailAddress.TryCreate(email, out _))))
        {
            ErrorMessage = "Enter a username, valid email, password, and role. Username must be at most 100 characters.";
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await LoadUsersAsync(cancellationToken);
            return Page();
        }

        try
        {
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            if (await UserExistsAsync(connection, username, email, null, cancellationToken))
            {
                ErrorMessage = "A user with that username or email already exists.";
                Response.StatusCode = StatusCodes.Status400BadRequest;
                await LoadUsersAsync(cancellationToken);
                return Page();
            }

            var passwordHash = BCrypt.Net.BCrypt.HashPassword(password, 10);
            await using var command = new MySqlCommand(
                "INSERT INTO users (username, email, full_name, password_hash, role, created_at) VALUES (@username, @email, @fullName, @passwordHash, @role, NOW())",
                connection);
            command.Parameters.AddWithValue("@username", username);
            command.Parameters.AddWithValue("@email", (object?)email ?? DBNull.Value);
            command.Parameters.AddWithValue("@fullName", (object?)fullName ?? DBNull.Value);
            command.Parameters.AddWithValue("@passwordHash", passwordHash);
            command.Parameters.AddWithValue("@role", role);
            await command.ExecuteNonQueryAsync(cancellationToken);

            TempData["StatusMessage"] = "The user account was created.";
            return Redirect("/admin/users");
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while creating a user account.");
            Response.StatusCode = exception.Number == 1062
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = exception.Number == 1062
                ? "A user with that username or email already exists."
                : "The user account could not be created. Check the shared database configuration.";
            await LoadUsersAsync(cancellationToken);
            return Page();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while creating a user account.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "The shared account database is not configured. Check the .NET and Node database configuration.";
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int userId, CancellationToken cancellationToken)
    {
        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId <= 0 || string.Equals(currentUserId, userId.ToString(), StringComparison.Ordinal))
        {
            ErrorMessage = "You cannot delete your own active administrator account.";
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await LoadUsersAsync(cancellationToken);
            return Page();
        }

        try
        {
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            await using var command = new MySqlCommand("DELETE FROM users WHERE id = @id", connection);
            command.Parameters.AddWithValue("@id", userId);

            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                return NotFound();
            }

            TempData["StatusMessage"] = "The user account was deleted.";
            return Redirect("/admin/users");
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while deleting a user account.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "The user account could not be deleted. Check the shared database configuration.";
            await LoadUsersAsync(cancellationToken);
            return Page();
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while deleting a user account.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "The shared account database is not configured. Check the .NET and Node database configuration.";
            return Page();
        }
    }

    internal static async Task<bool> UserExistsAsync(
        MySqlConnection connection,
        string username,
        string? email,
        int? exceptUserId,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM users WHERE (username = @username OR (@email IS NOT NULL AND email = @email)) AND (@exceptId IS NULL OR id <> @exceptId)",
            connection);
        command.Parameters.AddWithValue("@username", username);
        command.Parameters.AddWithValue("@email", (object?)email ?? DBNull.Value);
        command.Parameters.AddWithValue("@exceptId", (object?)exceptUserId ?? DBNull.Value);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private async Task<bool> LoadUsersAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            await using var command = new MySqlCommand(
                "SELECT id, username, email, full_name, role, created_at FROM users ORDER BY created_at DESC",
                connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                Users.Add(new ManagedUser(
                    reader.GetInt32("id"),
                    reader.GetString("username"),
                    reader.IsDBNull(reader.GetOrdinal("email")) ? null : reader.GetString("email"),
                    reader.IsDBNull(reader.GetOrdinal("full_name")) ? null : reader.GetString("full_name"),
                    reader.GetString("role"),
                    reader.GetDateTime("created_at")));
            }

            return true;
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while loading user accounts.");
            ErrorMessage = "Could not load users from the shared account database. Check the .NET and Node database configuration.";
            return false;
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while loading user accounts.");
            ErrorMessage = "The shared account database is not configured. Check the .NET and Node database configuration.";
            return false;
        }
    }
}

public sealed record ManagedUser(
    int Id,
    string Username,
    string? Email,
    string? FullName,
    string Role,
    DateTime CreatedAt);
