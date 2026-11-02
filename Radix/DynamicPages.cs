using MySqlConnector;

namespace dotnet_server;

public sealed class DynamicPages(RadixDatabase database)
{
    public async Task<DynamicPage?> FindBySlugAsync(string slug, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT id, title, slug, content, template_name, created_at, status, min_role, publish_at FROM pages WHERE slug = @slug LIMIT 1",
            connection);
        command.Parameters.AddWithValue("@slug", slug);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadPage(reader);
    }

    public async Task<IReadOnlyList<DynamicPage>> GetPagesAsync(bool trashOnly, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            trashOnly
                ? "SELECT id, title, slug, content, template_name, created_at, status, min_role, publish_at FROM pages WHERE status = 'Deleted' ORDER BY created_at DESC"
                : "SELECT id, title, slug, content, template_name, created_at, status, min_role, publish_at FROM pages WHERE status <> 'Deleted' ORDER BY created_at DESC",
            connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var pages = new List<DynamicPage>();
        while (await reader.ReadAsync(cancellationToken))
        {
            pages.Add(ReadPage(reader));
        }

        return pages;
    }

    public async Task<DynamicPage?> FindByIdAsync(int id, CancellationToken cancellationToken)
    {
        await using var connection = await database.OpenConnectionAsync(cancellationToken);
        await using var command = new MySqlCommand(
            "SELECT id, title, slug, content, template_name, created_at, status, min_role, publish_at FROM pages WHERE id = @id",
            connection);
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadPage(reader) : null;
    }

    public async Task<bool> SlugExistsAsync(
        MySqlConnection connection,
        string slug,
        int? exceptId,
        CancellationToken cancellationToken)
    {
        await using var command = new MySqlCommand(
            "SELECT COUNT(*) FROM pages WHERE slug = @slug AND (@exceptId IS NULL OR id <> @exceptId)",
            connection);
        command.Parameters.AddWithValue("@slug", slug);
        command.Parameters.AddWithValue("@exceptId", (object?)exceptId ?? DBNull.Value);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static DynamicPage ReadPage(MySqlDataReader reader) => new(
        reader.GetInt32("id"),
        reader.GetString("title"),
        reader.GetString("slug"),
        reader.GetString("content"),
        reader.IsDBNull(reader.GetOrdinal("template_name")) ? "default" : reader.GetString("template_name"),
        reader.GetDateTime("created_at"),
        reader.GetString("status"),
        reader.GetString("min_role"),
        reader.GetDateTime("publish_at"));
}

public sealed record DynamicPage(
    int Id,
    string Title,
    string Slug,
    string Content,
    string TemplateName,
    DateTime CreatedAt,
    string Status,
    string MinimumRole,
    DateTime PublishAt);
