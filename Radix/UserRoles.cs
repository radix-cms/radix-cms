namespace dotnet_server;

public static class UserRoles
{
    public static IReadOnlyList<string> All { get; } =
    [
        "Administrator",
        "Manager",
        "Editor",
        "Registered",
        "Unregistered"
    ];

    public static bool IsValid(string? role) => role is not null && All.Contains(role, StringComparer.Ordinal);
}
