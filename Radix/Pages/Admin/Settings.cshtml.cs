using System.ComponentModel.DataAnnotations;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MimeKit;
using MySqlConnector;

namespace dotnet_server.Pages.Admin;

[Authorize(Policy = "Administrator")]
public sealed class SettingsModel(
    RadixDatabase database,
    IConfiguration configuration,
    ILogger<SettingsModel> logger) : PageModel
{
    [BindProperty, StringLength(2048)]
    public string SiteLogo { get; set; } = string.Empty;

    [BindProperty, Required, StringLength(255)]
    public string MailHost { get; set; } = string.Empty;

    [BindProperty, Range(1, 65535)]
    public int MailPort { get; set; } = 25;

    [BindProperty, Required]
    public string MailSecure { get; set; } = "0";

    [BindProperty, StringLength(255)]
    public string MailUser { get; set; } = string.Empty;

    [BindProperty, StringLength(1024)]
    public string MailPassword { get; set; } = string.Empty;

    [BindProperty, EmailAddress, StringLength(255)]
    public string MailFrom { get; set; } = string.Empty;

    [BindProperty, EmailAddress, Required, StringLength(255)]
    public string RecipientEmail { get; set; } = string.Empty;

    public bool HasSavedMailPassword { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? SuccessMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        InitializeDefaults();
        try
        {
            var settings = await ReadSettingsAsync(cancellationToken);
            SiteLogo = GetSetting(settings, "site_logo", SiteLogo);
            MailHost = GetNonEmptySetting(settings, "mail_host", MailHost);
            MailPort = int.TryParse(GetSetting(settings, "mail_port", MailPort.ToString()), out var port)
                ? port
                : MailPort;
            MailSecure = GetNonEmptySetting(settings, "mail_secure", MailSecure) == "1" ? "1" : "0";
            MailUser = GetNonEmptySetting(settings, "mail_user", MailUser);
            MailFrom = GetNonEmptySetting(settings, "mail_from", MailFrom);
            HasSavedMailPassword = !string.IsNullOrEmpty(GetSetting(settings, "mail_pass"));
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while loading admin settings.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "Settings could not be loaded from the shared database.";
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while loading admin settings.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "The shared settings database is not configured.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync(CancellationToken cancellationToken)
    {
        NormalizeSettings();
        ValidateEncryption();
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Check the settings and try again.";
            MailPassword = string.Empty;
            ModelState.Remove(nameof(MailPassword));
            await LoadPasswordStateAsync(cancellationToken);
            return Page();
        }

        try
        {
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            if (string.IsNullOrEmpty(MailPassword))
            {
                MailPassword = await GetSettingAsync(connection, transaction, "mail_pass", cancellationToken);
            }

            var updates = new Dictionary<string, string>
            {
                ["site_logo"] = SiteLogo,
                ["mail_host"] = MailHost,
                ["mail_port"] = MailPort.ToString(),
                ["mail_secure"] = MailSecure,
                ["mail_user"] = MailUser,
                ["mail_pass"] = MailPassword,
                ["mail_from"] = MailFrom
            };

            foreach (var (key, value) in updates)
            {
                await using var command = new MySqlCommand(
                    "INSERT INTO settings (setting_key, setting_value) VALUES (@key, @value) ON DUPLICATE KEY UPDATE setting_value = @value",
                    connection,
                    transaction);
                command.Parameters.AddWithValue("@key", key);
                command.Parameters.AddWithValue("@value", value);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            MailPassword = string.Empty;
            ModelState.Remove(nameof(MailPassword));
            HasSavedMailPassword = !string.IsNullOrEmpty(updates["mail_pass"]);
            SuccessMessage = "Settings saved successfully.";
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while saving admin settings.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "Settings could not be saved to the shared database.";
            MailPassword = string.Empty;
            ModelState.Remove(nameof(MailPassword));
            await LoadPasswordStateAsync(cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while saving admin settings.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "The shared settings database is not configured.";
            MailPassword = string.Empty;
            ModelState.Remove(nameof(MailPassword));
            await LoadPasswordStateAsync(cancellationToken);
        }

        return Page();
    }

    public async Task<IActionResult> OnPostTestEmailAsync(CancellationToken cancellationToken)
    {
        NormalizeSettings();
        ModelState.Remove(nameof(SiteLogo));
        ModelState.Remove(nameof(MailHost));
        ModelState.Remove(nameof(MailPort));
        ModelState.Remove(nameof(MailSecure));
        ModelState.Remove(nameof(MailUser));
        ModelState.Remove(nameof(MailPassword));
        ModelState.Remove(nameof(MailFrom));
        if (!ModelState.IsValid)
        {
            ErrorMessage = "Enter a valid recipient email address.";
            await LoadPageSettingsAsync(cancellationToken);
            return Page();
        }

        try
        {
            var settings = await ReadSettingsAsync(cancellationToken);
            var host = GetNonEmptySetting(settings, "mail_host", configuration["Mail:Host"] ?? string.Empty);
            var portText = GetNonEmptySetting(settings, "mail_port", configuration["Mail:Port"] ?? "25");
            var username = GetNonEmptySetting(settings, "mail_user", configuration["Mail:User"] ?? string.Empty);
            var password = GetSetting(settings, "mail_pass", configuration["Mail:Password"] ?? string.Empty);
            var fromAddress = GetNonEmptySetting(settings, "mail_from", configuration["Mail:From"] ?? "noreply@radixcms.local");
            var secure = GetNonEmptySetting(
                settings,
                "mail_secure",
                bool.TryParse(configuration["Mail:Secure"], out var configuredSecure) && configuredSecure ? "1" : "0");

            if (string.IsNullOrWhiteSpace(host)
                || !int.TryParse(portText, out var port)
                || port is < 1 or > 65535
                || !MailboxAddress.TryParse(fromAddress, out var sender))
            {
                ErrorMessage = "Configure a valid SMTP host, port, and sender address before sending a test email.";
                await LoadPageSettingsAsync(cancellationToken);
                return Page();
            }

            var message = new MimeMessage();
            message.From.Add(sender);
            message.To.Add(MailboxAddress.Parse(RecipientEmail));
            message.Subject = "Radix CMS - SMTP Test Connection";
            message.Body = new TextPart("plain")
            {
                Text = "Your Radix CMS mail server settings are configured correctly."
            };

            using var client = new SmtpClient();
            var socketOptions = secure == "1"
                ? SecureSocketOptions.SslOnConnect
                : SecureSocketOptions.StartTlsWhenAvailable;
            await client.ConnectAsync(host, port, socketOptions, cancellationToken);
            if (!string.IsNullOrWhiteSpace(username))
            {
                await client.AuthenticateAsync(username, password, cancellationToken);
            }
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            SuccessMessage = $"Test email sent successfully to {RecipientEmail}.";
            await LoadPageSettingsAsync(cancellationToken);
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while loading SMTP settings for a test email.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "SMTP settings could not be loaded from the shared database.";
            await LoadPageSettingsAsync(cancellationToken);
        }
        catch (MailKit.Security.SslHandshakeException exception)
        {
            logger.LogError(exception, "SMTP TLS certificate validation failed while sending a test email.");
            ErrorMessage = "The SMTP server's TLS certificate could not be validated for the configured host. Use the DNS hostname covered by the certificate instead of its IP address, or have the mail administrator correct the certificate.";
            await LoadPageSettingsAsync(cancellationToken);
        }
        catch (MailKit.Security.AuthenticationException exception)
        {
            logger.LogError(exception, "SMTP authentication failed while sending a test email.");
            ErrorMessage = "SMTP authentication failed. Check the configured username and password.";
            await LoadPageSettingsAsync(cancellationToken);
        }
        catch (MailKit.Net.Smtp.SmtpCommandException exception)
        {
            logger.LogError(exception, "SMTP server rejected the test email.");
            ErrorMessage = "The SMTP server rejected the test email. Check the sender address and server settings.";
            await LoadPageSettingsAsync(cancellationToken);
        }
        catch (MailKit.Net.Smtp.SmtpProtocolException exception)
        {
            logger.LogError(exception, "SMTP protocol error while sending a test email.");
            ErrorMessage = "The SMTP server returned an unexpected response. Check its host, port, and encryption settings.";
            await LoadPageSettingsAsync(cancellationToken);
        }
        catch (MailKit.ServiceNotConnectedException exception)
        {
            logger.LogError(exception, "SMTP connection was lost while sending a test email.");
            ErrorMessage = "The SMTP server connection was lost before the test email could be sent.";
            await LoadPageSettingsAsync(cancellationToken);
        }
        catch (MailKit.ServiceNotAuthenticatedException exception)
        {
            logger.LogError(exception, "SMTP client was not authenticated while sending a test email.");
            ErrorMessage = "SMTP authentication is required. Check the configured username and password.";
            await LoadPageSettingsAsync(cancellationToken);
        }
        catch (System.Net.Sockets.SocketException exception)
        {
            logger.LogError(exception, "Could not connect to the SMTP server for a test email.");
            ErrorMessage = "Could not connect to the SMTP server. Check the host, port, and encryption settings.";
            await LoadPageSettingsAsync(cancellationToken);
        }
        catch (System.Security.Authentication.AuthenticationException exception)
        {
            logger.LogError(exception, "SMTP TLS negotiation failed while sending a test email.");
            ErrorMessage = "Could not establish a secure SMTP connection. Check the port and encryption settings.";
            await LoadPageSettingsAsync(cancellationToken);
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Configuration error while preparing an SMTP test email.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            ErrorMessage = "The SMTP configuration is invalid or the shared settings database is unavailable.";
            await LoadPageSettingsAsync(cancellationToken);
        }

        return Page();
    }

    private void InitializeDefaults()
    {
        MailHost = configuration["Mail:Host"] ?? string.Empty;
        MailPort = int.TryParse(configuration["Mail:Port"], out var port) ? port : 25;
        MailSecure = bool.TryParse(configuration["Mail:Secure"], out var secure) && secure ? "1" : "0";
        MailUser = configuration["Mail:User"] ?? string.Empty;
        MailFrom = configuration["Mail:From"] ?? "noreply@radixcms.local";
    }

    private void NormalizeSettings()
    {
        SiteLogo = SiteLogo.Trim();
        MailHost = MailHost.Trim();
        MailUser = MailUser.Trim();
        MailFrom = MailFrom.Trim();
        RecipientEmail = RecipientEmail.Trim();
    }

    private void ValidateEncryption()
    {
        if (MailSecure is not ("0" or "1"))
        {
            ModelState.AddModelError(nameof(MailSecure), "Select a valid encryption option.");
        }
    }

    private async Task<IReadOnlyDictionary<string, string>> ReadSettingsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT setting_key, setting_value FROM settings",
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var settings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(cancellationToken))
        {
            settings[reader.GetString("setting_key")] =
                reader.IsDBNull(reader.GetOrdinal("setting_value")) ? string.Empty : reader.GetString("setting_value");
        }

        return settings;
    }

    private async Task LoadPageSettingsAsync(CancellationToken cancellationToken)
    {
        ModelState.Clear();
        InitializeDefaults();
        try
        {
            var settings = await ReadSettingsAsync(cancellationToken);
            SiteLogo = GetSetting(settings, "site_logo", SiteLogo);
            MailHost = GetNonEmptySetting(settings, "mail_host", MailHost);
            MailPort = int.TryParse(GetSetting(settings, "mail_port", MailPort.ToString()), out var port)
                ? port
                : MailPort;
            MailSecure = GetNonEmptySetting(settings, "mail_secure", MailSecure) == "1" ? "1" : "0";
            MailUser = GetNonEmptySetting(settings, "mail_user", MailUser);
            MailFrom = GetNonEmptySetting(settings, "mail_from", MailFrom);
            HasSavedMailPassword = !string.IsNullOrEmpty(GetSetting(settings, "mail_pass"));
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while reloading admin settings.");
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while reloading admin settings.");
        }
    }

    private async Task LoadPasswordStateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var settings = await ReadSettingsAsync(cancellationToken);
            HasSavedMailPassword = !string.IsNullOrEmpty(GetSetting(settings, "mail_pass"));
        }
        catch (MySqlException exception)
        {
            logger.LogError(exception, "Database error while checking the saved SMTP password.");
        }
        catch (InvalidOperationException exception)
        {
            logger.LogError(exception, "Database configuration error while checking the saved SMTP password.");
        }
    }

    private static async Task<string> GetSettingAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        string key,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT setting_value FROM settings WHERE setting_key = @key",
            connection,
            transaction);
        command.Parameters.AddWithValue("@key", key);
        return Convert.ToString(await command.ExecuteScalarAsync(cancellationToken)) ?? string.Empty;
    }

    private static string GetSetting(
        IReadOnlyDictionary<string, string> settings,
        string key,
        string defaultValue = "")
        => settings.TryGetValue(key, out var value) ? value : defaultValue;

    private static string GetNonEmptySetting(
        IReadOnlyDictionary<string, string> settings,
        string key,
        string defaultValue)
        => settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : defaultValue;
}
