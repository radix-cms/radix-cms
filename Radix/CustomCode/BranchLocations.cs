using System.Globalization;
using System.Text;

namespace dotnet_server.CustomCode;

public sealed class BranchLocations(
    IWebHostEnvironment environment,
    ILogger<BranchLocations> logger)
{
    private static readonly string[] RequiredColumns =
    [
        "slug", "location name", "street", "city", "province", "postal code",
        "phone", "toll free", "email", "openingtime", "closingtime", "timezone", "opendays"
    ];

    private readonly SemaphoreSlim loadLock = new(1, 1);
    private IReadOnlyList<BranchLocation>? cache;

    public async Task<IReadOnlyList<BranchLocation>> GetAllAsync(CancellationToken cancellationToken)
    {
        if (cache is not null)
        {
            return cache;
        }

        await loadLock.WaitAsync(cancellationToken);
        try
        {
            if (cache is not null)
            {
                return cache;
            }

            var path = GetCsvPath();
            var lines = (await File.ReadAllLinesAsync(path, cancellationToken))
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
            if (lines.Length < 2)
            {
                throw new InvalidDataException($"No branch locations were found in {path}.");
            }

            var headers = ParseRow(lines[0], 1, path)
                .Select(header => header.Trim().TrimStart('\uFEFF').ToLowerInvariant())
                .ToArray();
            if (RequiredColumns.Any(column => !headers.Contains(column, StringComparer.Ordinal)))
            {
                throw new InvalidDataException($"Branch locations CSV is missing required columns: {path}.");
            }

            var indexes = RequiredColumns.ToDictionary(
                column => column,
                column => Array.IndexOf(headers, column),
                StringComparer.Ordinal);
            var locations = new List<BranchLocation>();
            for (var index = 1; index < lines.Length; index++)
            {
                var lineNumber = index + 1;
                var fields = ParseRow(lines[index], lineNumber, path);
                if (fields.Length != headers.Length)
                {
                    throw new InvalidDataException($"Expected {headers.Length} fields on line {lineNumber} of {path}.");
                }

                string Value(string column) => fields[indexes[column]];
                var slug = Value("slug").ToLowerInvariant();
                var openingTime = Value("openingtime");
                var closingTime = Value("closingtime");
                var timeZone = Value("timezone");
                var openDays = Value("opendays");

                if (!System.Text.RegularExpressions.Regex.IsMatch(slug, @"\A[a-z0-9-]+\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant)
                    || !TimeOnly.TryParseExact(openingTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                    || !TimeOnly.TryParseExact(closingTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                    || !IsValidOpenDays(openDays)
                    || !IsValidTimeZone(timeZone))
                {
                    throw new InvalidDataException($"Invalid branch location data on line {lineNumber} of {path}.");
                }

                locations.Add(new BranchLocation(
                    slug,
                    Value("location name"),
                    Value("street"),
                    Value("city"),
                    Value("province"),
                    Value("postal code"),
                    Value("phone"),
                    Value("toll free"),
                    Value("email"),
                    openingTime,
                    closingTime,
                    timeZone,
                    openDays,
                    FormatOpenDays(openDays)));
            }

            if (locations.Select(location => location.Slug).Distinct(StringComparer.OrdinalIgnoreCase).Count() != locations.Count)
            {
                throw new InvalidDataException($"Duplicate branch slug found in {path}.");
            }

            cache = locations;
            return cache;
        }
        catch (IOException exception)
        {
            logger.LogError(exception, "Could not read AG branch locations.");
            throw;
        }
        catch (InvalidDataException exception)
        {
            logger.LogError(exception, "AG branch locations CSV contains invalid data.");
            throw;
        }
        finally
        {
            loadLock.Release();
        }
    }

    public async Task<BranchLocation?> FindBySlugAsync(string slug, CancellationToken cancellationToken)
        => (await GetAllAsync(cancellationToken))
            .FirstOrDefault(location => location.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

    private string GetCsvPath()
    {
        var contentRootCsv = Path.Combine(environment.ContentRootPath, "Data", "ag-locations.csv");
        if (File.Exists(contentRootCsv))
        {
            return contentRootCsv;
        }

        var outputCsv = Path.Combine(AppContext.BaseDirectory, "Data", "ag-locations.csv");
        if (File.Exists(outputCsv))
        {
            return outputCsv;
        }

        throw new FileNotFoundException("AG branch locations CSV was not found.", outputCsv);
    }

    private static string[] ParseRow(string line, int lineNumber, string path)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        var insideQuotes = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (insideQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }
            }
            else if (character == ',' && !insideQuotes)
            {
                fields.Add(field.ToString().Trim());
                field.Clear();
            }
            else
            {
                field.Append(character);
            }
        }

        if (insideQuotes)
        {
            throw new InvalidDataException($"Unclosed quoted CSV field on line {lineNumber} of {path}.");
        }

        fields.Add(field.ToString().Trim());
        return fields.ToArray();
    }

    private static bool IsValidOpenDays(string value)
        => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .All(range =>
            {
                var parts = range.Split('-');
                return parts.Length is 1 or 2
                    && int.TryParse(parts[0], out var start)
                    && start is >= 0 and <= 6
                    && (parts.Length == 1
                        || int.TryParse(parts[1], out var end) && end is >= 0 and <= 6 && start <= end);
            })
            && value.Length > 0;

    private static bool IsValidTimeZone(string value)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(value);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static string FormatOpenDays(string value)
    {
        var dayNames = new[] { "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday" };
        var days = new SortedSet<int>();
        foreach (var range in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = range.Split('-');
            var start = int.Parse(parts[0], CultureInfo.InvariantCulture);
            var end = parts.Length == 1 ? start : int.Parse(parts[1], CultureInfo.InvariantCulture);
            for (var day = start; day <= end; day++)
            {
                days.Add(day);
            }
        }

        if (days.SetEquals([1, 2, 3, 4, 5]))
        {
            return "Monday–Friday";
        }
        if (days.Count == 7)
        {
            return "Every day";
        }

        return string.Join(", ", days.Select(day => dayNames[day]));
    }
}

public sealed record BranchLocation(
    string Slug,
    string Name,
    string Street,
    string City,
    string Province,
    string PostalCode,
    string Phone,
    string TollFree,
    string Email,
    string OpeningTime,
    string ClosingTime,
    string TimeZone,
    string OpenDays,
    string OpenDaysLabel);
