using Microsoft.Data.SqlClient;

namespace dotnet_server.CustomCode;

public sealed class WestbankProjectPasswords(
    IConfiguration configuration,
    ILogger<WestbankProjectPasswords> logger)
{
    public async Task<IReadOnlyList<WestbankProject>> GetAllAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(
            "SELECT ID, ProjectName, Account, Password FROM DB_WestbankProjects ORDER BY Account",
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var projects = new List<WestbankProject>();
        while (await reader.ReadAsync(cancellationToken))
        {
            projects.Add(new WestbankProject(
                reader.GetInt32(reader.GetOrdinal("ID")),
                reader.GetString(reader.GetOrdinal("ProjectName")),
                reader.GetInt32(reader.GetOrdinal("Account")),
                reader.GetString(reader.GetOrdinal("Password"))));
        }

        return projects;
    }

    public async Task CreateAsync(
        int account,
        string password,
        string projectName,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(
            "INSERT INTO DB_WestbankProjects (ProjectName, Account, Password) VALUES (@projectName, @account, @password)",
            connection);
        AddProjectParameters(command, account, password, projectName);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> UpdateAsync(
        int id,
        int account,
        string password,
        string projectName,
        CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(
            "UPDATE DB_WestbankProjects SET ProjectName = @projectName, Account = @account, Password = @password WHERE ID = @id",
            connection);
        AddProjectParameters(command, account, password, projectName);
        command.Parameters.Add("@id", System.Data.SqlDbType.Int).Value = id;
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = new SqlCommand(
            "DELETE FROM DB_WestbankProjects WHERE ID = @id",
            connection);
        command.Parameters.Add("@id", System.Data.SqlDbType.Int).Value = id;
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var section = configuration.GetSection("mssql_intranet");
        var server = section["server"];
        var database = section["database"];
        var username = section["user"];
        var password = section["password"];
        if (string.IsNullOrWhiteSpace(server)
            || string.IsNullOrWhiteSpace(database)
            || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Configure mssql_intranet server, database, user, and password to use Westbank project passwords.");
        }

        var portValue = section["port"];
        var dataSource = server;
        if (!string.IsNullOrWhiteSpace(portValue))
        {
            if (!ushort.TryParse(portValue, out var port))
            {
                throw new InvalidOperationException("The mssql_intranet port must be a valid TCP port.");
            }

            dataSource = $"{server},{port}";
        }

        var options = section.GetSection("options");
        var encrypt = !bool.TryParse(options["encrypt"], out var configuredEncrypt) || configuredEncrypt;
        var trustServerCertificate =
            bool.TryParse(options["trustServerCertificate"], out var configuredTrust) && configuredTrust;
        logger.LogInformation(
            "Opening Westbank SQL connection with encryption {Encrypt} and certificate trust bypass {TrustServerCertificate}.",
            encrypt,
            trustServerCertificate);
        var connection = new SqlConnection(new SqlConnectionStringBuilder
        {
            DataSource = dataSource,
            InitialCatalog = database,
            UserID = username,
            Password = password,
            Encrypt = encrypt,
            TrustServerCertificate = trustServerCertificate
        }.ConnectionString);

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

    private static void AddProjectParameters(
        SqlCommand command,
        int account,
        string password,
        string projectName)
    {
        command.Parameters.Add("@projectName", System.Data.SqlDbType.NVarChar, 255).Value = projectName;
        command.Parameters.Add("@account", System.Data.SqlDbType.Int).Value = account;
        command.Parameters.Add("@password", System.Data.SqlDbType.NVarChar, 255).Value = password;
    }
}

public sealed record WestbankProject(int Id, string ProjectName, int Account, string Password);
