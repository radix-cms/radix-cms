using MySqlConnector;

namespace dotnet_server;

public sealed class RadixDatabase(IConfiguration configuration)
{
    public async Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connectionString = configuration.GetConnectionString("Radix");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var database = configuration["DB_NAME"] ?? configuration["Database:Name"];
            var username = configuration["DB_USER"] ?? configuration["Database:User"];
            var password = configuration["DB_PASSWORD"] ?? configuration["Database:Password"];

            if (string.IsNullOrWhiteSpace(database) || string.IsNullOrWhiteSpace(username) || password is null)
            {
                throw new InvalidOperationException(
                    "Database settings are missing. Configure ConnectionStrings:Radix or Database:Host, Database:Port, Database:Name, Database:User, and Database:Password.");
            }

            var configuredPortValue = configuration["DB_PORT"] ?? configuration["Database:Port"];
            var port = ushort.TryParse(configuredPortValue, out var configuredPort) ? configuredPort : (ushort)3306;
            var builder = new MySqlConnectionStringBuilder
            {
                Server = configuration["DB_HOST"] ?? configuration["Database:Host"] ?? "127.0.0.1",
                Port = port,
                Database = database,
                UserID = username,
                Password = password,
                ConnectionTimeout = 5
            };
            connectionString = builder.ConnectionString;
        }

        var connection = new MySqlConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
